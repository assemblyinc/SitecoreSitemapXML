﻿/* *********************************************************************** *
 * File   : SitemapManager.cs                             Part of Sitecore *
 * Version: 1.0.0                                         www.sitecore.net *
 *                                                                         *
 *                                                                         *
 * Purpose: Manager class what contains all main logic                     *
 *                                                                         *
 * Copyright (C) 1999-2009 by Sitecore A/S. All rights reserved.           *
 *                                                                         *
 * This work is the property of:                                           *
 *                                                                         *
 *        Sitecore A/S                                                     *
 *        Meldahlsgade 5, 4.                                               *
 *        1613 Copenhagen V.                                               *
 *        Denmark                                                          *
 *                                                                         *
 * This is a Sitecore published work under Sitecore's                      *
 * shared source license.                                                  *
 *                                                                         *
 * *********************************************************************** */

using Sitecore;
using Sitecore.Buckets.Managers;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Sites;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Sitemap.XML.Models
{
    public class SitemapManager
    {
        #region Fields 

        private readonly SitemapManagerConfiguration _config;

        #endregion

        #region Constructor

        public SitemapManager(SitemapManagerConfiguration config)
        {
            Assert.IsNotNull(config, "config");
            _config = config;
            if (!string.IsNullOrWhiteSpace(_config.FileName))
            {
                BuildSiteMap();
            }
        }

        #endregion

        #region Properties

        public Database Db
        {
            get
            {
                var database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
                return database;
            }
        }

        #endregion

        #region Private Methods

        private static IEnumerable<Item> GetSharedContentDefinitions()
        {
            var siteNode = GetContextSiteDefinitionItem();
            if (siteNode == null || string.IsNullOrWhiteSpace(siteNode.Name)) return null;

            var sharedDefinitions = siteNode.Children;
            return sharedDefinitions;
        }

        private static Item GetContextSiteDefinitionItem()
        {
            var database = Context.Database;
#if DEBUG
            database = Factory.GetDatabase("master");
#endif
            var sitemapModuleItem = database.GetItem(Constants.SitemapModuleSettingsRootItemId);
            var contextSite = Context.GetSiteName().ToLower();
            if (!sitemapModuleItem.Children.Any()) return null;
            var siteNode = sitemapModuleItem.Children.FirstOrDefault(i => i.Key == contextSite);
            return siteNode;
        }

        private string BuildSitemapXML(List<SitemapItem> items, Site site)
        {
            var doc = new XmlDocument();

            XmlNode declarationNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(declarationNode);
            XmlNode urlsetNode = doc.CreateElement("urlset");
            var xmlnsAttr = doc.CreateAttribute("xmlns");
            xmlnsAttr.Value = SitemapManagerConfiguration.XmlnsTpl;
            urlsetNode.Attributes.Append(xmlnsAttr);

            var xmlnsXhtmlTpl = doc.CreateAttribute("xmlns:xhtml");
            xmlnsXhtmlTpl.Value = SitemapManagerConfiguration.XmlnsXhtmlTpl;
            urlsetNode.Attributes.Append(xmlnsXhtmlTpl);

            doc.AppendChild(urlsetNode);

            foreach (var itm in items)
            {
                doc = BuildSitemapItem(doc, itm);
            }

            return doc.OuterXml;
        }

        private XmlDocument BuildSitemapItem(XmlDocument doc, SitemapItem item)
        {
            var urlsetNode = doc.LastChild;

            XmlNode urlNode = doc.CreateElement("url");
            urlsetNode.AppendChild(urlNode);

            XmlNode locNode = doc.CreateElement("loc");
            urlNode.AppendChild(locNode);
            locNode.AppendChild(doc.CreateTextNode(item.Location));

            if (SitemapManagerConfiguration.IncludeLastModInXml)
            {
                XmlNode lastmodNode = doc.CreateElement("lastmod");
                urlNode.AppendChild(lastmodNode);
                lastmodNode.AppendChild(doc.CreateTextNode(item.LastModified));
            }

            if (item.HrefLangs != null && item.HrefLangs.Count > 0)
            {
                foreach (var hrefLang in item.HrefLangs)
                {
                    var xmlElement = doc.CreateElement("xhtml", "link", SitemapManagerConfiguration.XmlnsXhtmlTpl);
                    var xmlAttribute = doc.CreateAttribute("rel");
                    xmlAttribute.Value = "alternate";
                    xmlElement.Attributes.Append(xmlAttribute);
                    xmlAttribute = doc.CreateAttribute("hreflang");
                    xmlAttribute.Value = hrefLang.HrefLang;
                    xmlElement.Attributes.Append(xmlAttribute);
                    xmlAttribute = doc.CreateAttribute("href");
                    xmlAttribute.Value = hrefLang.Href;
                    xmlElement.Attributes.Append(xmlAttribute);
                    urlNode.AppendChild(xmlElement);
                }
            }

            if (!string.IsNullOrWhiteSpace(item.ChangeFrequency))
            {
                XmlNode changeFrequencyNode = doc.CreateElement("changefreq");
                urlNode.AppendChild(changeFrequencyNode);
                changeFrequencyNode.AppendChild(doc.CreateTextNode(item.ChangeFrequency));
            }

            if (!string.IsNullOrWhiteSpace(item.Priority))
            {
                var priorityNode = doc.CreateElement("priority");
                urlNode.AppendChild(priorityNode);
                priorityNode.AppendChild(doc.CreateTextNode(item.Priority));
            }

            return doc;
        }

        private void SubmitEngine(string engine, string sitemapUrl)
        {
            //Check if it is not localhost because search engines returns an error
            if (!sitemapUrl.Contains("http://localhost"))
            {
                var request = string.Concat(engine, SitemapItem.HtmlEncode(sitemapUrl));

                var httpRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(request);
                try
                { 
                    var webResponse = httpRequest.GetResponse();

                    var httpResponse = (System.Net.HttpWebResponse)webResponse;
                    if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        Log.Error(string.Format("Cannot submit sitemap to \"{0}\"", engine), this);
                    }
                }
                catch
                {
                    Log.Warn(string.Format("The serachengine \"{0}\" returns an 404 error", request), this);
                }
            }
        }

        private void BuildSiteMap()
        {
            var site = SiteManager.GetSite(_config.SiteName);
            var siteContext = Factory.GetSite(_config.SiteName);
            var rootPath = siteContext.StartPath;

            var items = GetSitemapItems(rootPath);

            var fullPath = MainUtil.MapPath(string.Concat("/", _config.FileName));
            var xmlContent = BuildSitemapXML(items, site);

            var strWriter = new StreamWriter(fullPath, false);
            strWriter.Write(xmlContent);
            strWriter.Close();
        }

        private List<SitemapItem> GetSitemapItems(string rootPath)
        {
            var disTpls = _config.EnabledTemplates;
            var excludeItemsField = _config.ExcludedItems;

            var database = Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);

            //Get the content root item
            var contentRoot = database.Items[rootPath];

            //Get the descendents of the content root
            IEnumerable<Item> descendants;
            var user = Sitecore.Security.Accounts.User.FromName(Constants.SitemapParserUser, true);
            using (new Sitecore.Security.Accounts.UserSwitcher(user))
            {
                descendants = contentRoot.Axes.GetDescendants()
                    .Where(i => i[Constants.XmlSettings.ExcludeItemFromSitemap] != "1");
            }

            // getting shared content
            //TODO: Unverified and un-tested. Might need some modifications before it can be used.
            var sharedModels = new List<List<SitemapItem>>();
            var sharedDefinitions = Db.SelectItems(string.Format("fast:{0}/*", _config.SitemapConfigurationItemPath).Replace("-","#-#"));
            var site = Factory.GetSite(_config.SiteName);
            var enabledTemplates = BuildListFromString(disTpls, '|');
	        var excludeItems = BuildListFromString(excludeItemsField, '|');
            foreach (var sharedDefinition in sharedDefinitions)
            {
                if (string.IsNullOrWhiteSpace(sharedDefinition[Constants.SharedContent.ContentLocationFieldName]) ||
                    string.IsNullOrWhiteSpace(sharedDefinition[Constants.SharedContent.ParentItemFieldName]))
                    continue;
                var contentLocation = ((DatasourceField)sharedDefinition.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem;
                var parentItem = ((DatasourceField)sharedDefinition.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem;
                var sharedItems = new List<Item>();
                if (BucketManager.IsBucket(contentLocation))
                {
                    var index = ContentSearchManager.GetIndex(new SitecoreIndexableItem(contentLocation));
                    using (var searchContext = index.CreateSearchContext())
                    {
                        var searchResultItem =
                            searchContext.GetQueryable<SearchResultItem>()
                                .Where(item => item.Paths.Contains(contentLocation.ID) && item.ItemId != contentLocation.ID)
                                .ToList();
                        sharedItems.AddRange(searchResultItem.Select(i => i.GetItem()));
                    }
                }
                else
                {
                    sharedItems.AddRange(contentLocation.Axes.GetDescendants());
                }

                var cleanedSharedItems = from itm in sharedItems
                                         where itm.Template != null && enabledTemplates.Select(t => t.ToLower()).Contains(itm.Template.ID.ToString().ToLower())
                                                                    && !excludeItems.Contains(itm.ID.ToString())
                                         select itm;
                var sharedSitemapItems = cleanedSharedItems.Select(i => SitemapItem.GetLanguageSitemapItems(i, site, parentItem, _config));
                sharedModels.AddRange(sharedSitemapItems);
            }

            //All content items
            var contentItems = descendants.ToList();
            contentItems.Insert(0, contentRoot);

            //Filter out the content items that belongs to Sitemap Configuration Manager
            //Should belong to enabled templates
            //Should not be an excluded item
            var selected = from itm in contentItems
                where itm.Template != null && enabledTemplates.Contains(itm.Template.ID.ToString()) &&
                      !excludeItems.Contains(itm.ID.ToString())
                select itm;

            //Get sitemap items in Language blocks per item
            var languageSitemapItems = selected.Select(i => SitemapItem.GetLanguageSitemapItems(i, site, null, _config)).ToList();

            //Final list of sitamap items
            var sitemapItems = new List<SitemapItem>();

            //Adding shared items to the sitemap items list
            foreach (var sharedModel in sharedModels)
            {
                sitemapItems.AddRange(sharedModel);
            }

            //Adding language specific sitemap items to the sitemap items list
            foreach (var laguageSitemapItem in languageSitemapItems)
            {
                sitemapItems.AddRange(laguageSitemapItem);
            }

            //Order the items based on priority in descending order with a cap of max items
            sitemapItems = sitemapItems.OrderByDescending(u => u.Priority).Take(int.Parse(Constants.XmlSettings.UrlLimit)).ToList();

            return sitemapItems;
        }

        private static List<string> BuildListFromString(string str, char separator)
        {
            var separatedValues = str.Split(separator);
            var selected = from dtp in separatedValues
                           where !string.IsNullOrEmpty(dtp)
                           select dtp;

            var result = selected.ToList();

            return result;
        }

        #region View Helpers

        public static bool IsUnderContent(Item item)
        {
            return Context.Database.GetItem(Context.Site.StartPath).Axes.IsAncestorOf(item);
        }

        public static bool IsShared(Item item)
        {
            var sharedDefinitions = GetSharedContentDefinitions();
            if (sharedDefinitions == null) return false;
            var sharedItemContentRoots =
                sharedDefinitions.Select(i => ((DatasourceField)i.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem).ToList();
            if (!sharedItemContentRoots.Any()) return false;

            return sharedItemContentRoots.Any(i => i.ID == item.ID);
        }

        public static bool SitemapDefinitionExists()
        {
            var sitemapModuleSettingsItem = Context.Database.GetItem(Constants.SitemapModuleSettingsRootItemId);
            var siteDefinition = sitemapModuleSettingsItem.Children[Context.Site.Name];
            return siteDefinition != null;
        }

        public static Item GetContentLocation(Item item)
        {
            var sharedNodes = GetSharedContentDefinitions();
            var contentParent = sharedNodes
                .Where(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem.Axes.IsAncestorOf(item))
                .Select(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem)
                .FirstOrDefault();
            return contentParent;
        }

        public static bool IsChildUnderSharedLocation(Item child)
        {
            var sharedNodes = GetSharedContentDefinitions();
            var sharedContentLocations = sharedNodes.Select(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem);
            var isUnderShared = sharedContentLocations.Any(l => l.Axes.IsAncestorOf(child));
            return isUnderShared;
        }

        public static Item GetSharedLocationParent(Item child)
        {
            var sharedNodes = GetSharedContentDefinitions();
            var parent = sharedNodes
                .Where(n => ((DatasourceField)n.Fields[Constants.SharedContent.ContentLocationFieldName]).TargetItem.Axes.IsAncestorOf(child))
                .Select(n => ((DatasourceField)n.Fields[Constants.SharedContent.ParentItemFieldName]).TargetItem)
                .FirstOrDefault();
            return parent;
        }

        public static bool IsEnabledTemplate(Item item)
        {
            var config = new SitemapManagerConfiguration(Context.GetSiteName());
            return config.EnabledTemplates.ToLower().Contains(item.TemplateID.ToGuid().ToString());
        }

        public static bool IsExcludedItem(Item item)
        {
            var config = new SitemapManagerConfiguration(Context.GetSiteName());
            return config.ExcludedItems.ToLower().Contains(item.ID.ToGuid().ToString());
        }

        public static bool ContainsItemsToShow(IEnumerable<Item> items)
        {
            return items == null
                ? false
                : items.Any() && items.Any(IsEnabledTemplate) && items.Count(IsExcludedItem) < items.Count();
        }

        #endregion

        #endregion

        #region Public Members

        public string BuildSiteMapForHandler()
        {
            var site = Sitecore.Sites.SiteManager.GetSite(Sitecore.Context.Site.Name);
            var siteContext = Factory.GetSite(Sitecore.Context.Site.Name);
            var rootPath = siteContext.StartPath;

            var items = GetSitemapItems(rootPath);

            var xmlContent = this.BuildSitemapXML(items, site);
            return xmlContent;
        }
        
        public bool SubmitSitemapToSearchenginesByHttp()
        {
            if (!SitemapManagerConfiguration.IsProductionEnvironment)
                return false;

            var result = false;
            var sitemapConfig = Db.Items[_config.SitemapConfigurationItemPath];

            if (sitemapConfig != null)
            {
                //TODO: URL
                var engines = sitemapConfig.Fields[Constants.WebsiteDefinition.SearchEnginesFieldName].Value;
                var filePath = !_config.ServerUrl.EndsWith("/")
                            ? _config.ServerUrl + "/" + _config.FileName
                            : _config.ServerUrl + _config.FileName;
                foreach (var id in engines.Split('|'))
                {
                    var engine = Db.Items[id];
                    if (engine != null)
                    {
                        string engineHttpRequestString = engine.Fields[Constants.SitemapSubmissionUriFieldName].Value;
                        SubmitEngine(engineHttpRequestString, filePath);
                    }
                }
                result = true;
            }

            return result;
        }

        public void RegisterSitemapToRobotsFile()
        {
            if (string.IsNullOrWhiteSpace(_config.FileName)) return;
            var robotsPath = MainUtil.MapPath(string.Concat("/", Constants.RobotsFileName));
            var sitemapContent = new StringBuilder(string.Empty);
            
            if (File.Exists(robotsPath))
            {
                var sr = new StreamReader(robotsPath);
                sitemapContent.Append(sr.ReadToEnd());
                sr.Close();
            }
            else
            {
                sitemapContent.AppendLine("User-agent: *");
                sitemapContent.AppendLine("Disallow:");
            }

            var sw = new StreamWriter(robotsPath, false);
            var sitemapUrl = _config.ServerUrl + "/" + _config.FileName;
            var sitemapLine = string.Concat("Sitemap: ", sitemapUrl);
            if (!sitemapContent.ToString().Contains(sitemapLine))
            {
                sitemapContent.AppendLine(sitemapLine);
            }
            sw.Write(sitemapContent.ToString());
            sw.Close();
        }

        public string GetRobotSite()
        {
            var sitemapContent = new StringBuilder(string.Empty);
            sitemapContent.AppendLine("User-agent: *");
            sitemapContent.AppendLine("Disallow:");
            var sitemapUrl = _config.ServerUrl + "/" + _config.SitemapNameForRobots;
            var sitemapLine = string.Concat("Sitemap: ", sitemapUrl);
            if (!sitemapContent.ToString().Contains(sitemapLine))
            {
                sitemapContent.AppendLine(sitemapLine);
            }
            return sitemapContent.ToString();
        }
        #endregion
    }
}
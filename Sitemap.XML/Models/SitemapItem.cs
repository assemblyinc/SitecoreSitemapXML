using Sitecore.Data.Items;
using Sitecore.Sites;
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Sitecore.Globalization;
using Sitecore.Links;
using System.Linq;

namespace Sitemap.XML.Models
{
    public class SitemapItem
    {
        #region Constructor

        public SitemapItem(Item item, SiteContext site, Item parentItem, SitemapManagerConfiguration config,
            Language currentLanguage = null, List<Language> allLanguages = null)
        {
            var itemId = item.ID;
            var database = Sitecore.Configuration.Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);

            Id = itemId.Guid;

            allLanguages = allLanguages ?? item.Languages.Where(l =>
                config.EnabledLanguageList == null ||
                config.EnabledLanguageList.Contains(l.Origin.ItemId.ToString())).ToList();

            var currentLanguageName = !string.IsNullOrWhiteSpace(site.Language)
                ? site.Language
                : allLanguages.FirstOrDefault().Name;

            currentLanguageName = currentLanguage != null ? currentLanguage.Name : currentLanguageName;

            HrefLangs = new List<SitemapItemHrefLang>();

            foreach (var language in allLanguages)
            {
                item = database.GetItem(itemId, language);

                var itemUrl = HtmlEncode(GetItemUrl(item, site, config, language));
                if (parentItem != null)
                {
                    itemUrl = GetSharedItemUrl(item, site, parentItem, config);
                }

                if (currentLanguage != null && language.Name == currentLanguage.Name)
                {
                    Priority = item[Constants.SeoSettings.Priority];
                    ChangeFrequency = item[Constants.SeoSettings.ChangeFrequency].ToLower();
                    LastModified = HtmlEncode(item.Statistics.Updated.ToLocalTime()
                        .ToString(Constants.XmlSettings.LastmodDateFormat));
                    Location = language.Name == currentLanguageName ? itemUrl : Location;
                }

                HrefLangs.Add(new SitemapItemHrefLang()
                {
                    Href = itemUrl,
                    HrefLang = language.Name
                });
            }
        }

        #endregion

        #region Properties

        public string Location { get; set; }
        public string LastModified { get; set; }
        public string ChangeFrequency { get; set; }
        public string Priority { get; set; }
        public Guid Id { get; set; }
        public List<SitemapItemHrefLang> HrefLangs { get; set; }

        #endregion

        #region Private Methods

        private static string GetSharedItemUrl(Item item, SiteContext site, Item parentItem, SitemapManagerConfiguration config)
        {
            var itemUrl = HtmlEncode(GetItemUrl(item, site, config));
            var parentUrl = HtmlEncode(GetItemUrl(parentItem, site, config));
            var siteConfig = new SitemapManagerConfiguration(site.Name);
            parentUrl = parentUrl.EndsWith("/") ? parentUrl : parentUrl + "/";
            if (siteConfig.CleanupBucketPath)
            {
                var pos = itemUrl.LastIndexOf("/", StringComparison.Ordinal) + 1;
                var itemNamePath = itemUrl.Substring(pos, itemUrl.Length - pos);
                return HtmlEncode(parentUrl + itemNamePath);
            }
            else
            {
                var contentParentItem = SitemapManager.GetContentLocation(item);
                if (contentParentItem == null) return null;
                var contentParentItemUrl = HtmlEncode(GetItemUrl(contentParentItem, site, config));
                if (string.IsNullOrWhiteSpace(contentParentItemUrl)) return string.Empty;
                itemUrl = itemUrl.Replace(contentParentItemUrl, string.Empty);
                return string.IsNullOrWhiteSpace(itemUrl) ? string.Empty : HtmlEncode(parentUrl + itemUrl.Trim('/'));
            }
        }

        public static string GetSharedItemUrl(Item item, SiteContext site, SitemapManagerConfiguration config)
        {
            var parentItem = SitemapManager.GetSharedLocationParent(item);
            var itemUrl = HtmlEncode(GetItemUrl(item, site, config));
            var parentUrl = HtmlEncode(GetItemUrl(parentItem, site, config));
            parentUrl = parentUrl.EndsWith("/") ? parentUrl : parentUrl + "/";
            var pos = itemUrl.LastIndexOf("/") + 1;
            var itemNamePath = itemUrl.Substring(pos, itemUrl.Length - pos);
            return HtmlEncode(parentUrl + itemNamePath);
        }

        #endregion

        #region Public Methods

        public static string HtmlEncode(string text)
        {
            return HttpUtility.HtmlEncode(text);
        }

        public static List<SitemapItem> GetLanguageSitemapItems(Item item, SiteContext site, Item parentItem, SitemapManagerConfiguration config)
        {
            //If list of languages are not specified take all languages or else take only the languages in the list.
            var languages = item.Languages.Where(l =>
                    config.EnabledLanguageList == null ||
                    config.EnabledLanguageList.Contains(l.Origin.ItemId.ToString()))
                .ToList();

            return languages.Select(language => new SitemapItem(item, site, parentItem, config, language, languages)).ToList();
        }

        public static string GetItemUrl(Item item, SiteContext site, SitemapManagerConfiguration config,  Language language = null)
        {
            var options = LinkManager.GetDefaultUrlBuilderOptions();
            
            options.SiteResolving = Sitecore.Configuration.Settings.Rendering.SiteResolving;
            options.Site = SiteContext.GetSite(site.Name);
            options.AlwaysIncludeServerUrl = false;
            options.UseDisplayName = config.UseDisplayName ;
            if (language != null)
            {
                options.LanguageEmbedding = config.EnableLanguageEmbedding? LanguageEmbedding.Always: LanguageEmbedding.Never;
                options.Language = language;
            }

            var url = LinkManager.GetItemUrl(item, options);

            //Sitecore OOTB does not use display name for the home page URLs even if configured.
            //That needs to be corrected
            if (item.Paths.FullPath.Equals(site.StartPath) && !url.EndsWith(item.DisplayName) && config.UseDisplayName)
            {
                url = string.Concat(url, url.EndsWith("/")? "": "/", item.DisplayName);
            }

            var serverUrl = config.ServerUrl;

            var isHttps = false;
            
            if (serverUrl.Contains("http://"))
            {
                serverUrl = serverUrl.Substring("http://".Length);
            }
            else if (serverUrl.Contains("https://"))
            {
                serverUrl = serverUrl.Substring("https://".Length);
                isHttps = true;
            }

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(serverUrl))
            {
                if (url.Contains("://") && !url.Contains("http"))
                {
                    sb.Append(isHttps ? "https://" : "http://");
                    sb.Append(serverUrl);
                    if (url.IndexOf("/", 3) > 0)
                        sb.Append(url.Substring(url.IndexOf("/", 3)));
                }
                else
                {
                    sb.Append(isHttps ? "https://" : "http://");
                    sb.Append(serverUrl);
                    sb.Append(url);
                }
            }
            else if (!string.IsNullOrEmpty(site.Properties["hostname"]))
            {
                sb.Append(isHttps ? "https://" : "http://");
                sb.Append(site.Properties["hostname"]);
                sb.Append(url);
            }
            else
            {
                if (url.Contains("://") && !url.Contains("http"))
                {
                    sb.Append(isHttps ? "https://" : "http://");
                    sb.Append(url);
                }
                else
                {
                    sb.Append(Sitecore.Web.WebUtil.GetFullUrl(url));
                }
            }
            return sb.ToString();
        }

        #endregion
    }
}
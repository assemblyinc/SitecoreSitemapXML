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

        public SitemapItem() {
        }

        public SitemapItem(Item item, SiteContext site, Item parentItem, SitemapManagerConfiguration config)
        {
            Priority = item[Constants.SeoSettings.Priority];
            ChangeFrequency = item[Constants.SeoSettings.ChangeFrequency].ToLower();
            LastModified = HtmlEncode(item.Statistics.Updated.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"));
            Id = item.ID.Guid;
            Title = item[Constants.SeoSettings.Title];

            var languages = item.Languages.Where(l => config.EnabledLanguageList == null ||
                                                      config.EnabledLanguageList.Contains(l.Origin.ItemId.ToString())).ToArray();

            var siteLanguage = !string.IsNullOrWhiteSpace(site.Language) ? site.Language : languages.FirstOrDefault().Name;

	        HrefLangs = new List<SitemapItemHrefLang>();
            
            foreach (var language in languages)
            {
                var sharedItemUrl = HtmlEncode(GetItemUrl(item, site, config, language));
                if (parentItem != null)
                {
                    sharedItemUrl = GetSharedItemUrl(item, site, parentItem, config);
                }

                Location = language.Name == siteLanguage ? sharedItemUrl : Location;

                HrefLangs.Add(new SitemapItemHrefLang()
                {
                    Href = sharedItemUrl,
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
        public string Title { get; set; }
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
            var result = HttpUtility.HtmlEncode(text);
            return result;
        }

        public static string GetItemUrl(Item item, SiteContext site, SitemapManagerConfiguration config,  Language language = null)
        {
            var options = Sitecore.Links.UrlOptions.DefaultOptions;

            options.SiteResolving = Sitecore.Configuration.Settings.Rendering.SiteResolving;
            options.Site = SiteContext.GetSite(site.Name);
            options.AlwaysIncludeServerUrl = false;
            options.UseDisplayName = config.UseDisplayName == "1" ;
            if (language != null)
            {
                options.LanguageEmbedding = LanguageEmbedding.Always;
                options.Language = language;

                Sitecore.Data.Database database = Sitecore.Configuration.Factory.GetDatabase(SitemapManagerConfiguration.WorkingDatabase);
                item = database.GetItem(item.ID, language);
            }

			var url = Sitecore.Links.LinkManager.GetItemUrl(item, options);

            var serverUrl = config.ServerUrl;

            var isHttps = false;
            
            if (serverUrl.Contains("http://"))
            {
				serverUrl = serverUrl.Substring("http://".Length);
				isHttps = false;
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
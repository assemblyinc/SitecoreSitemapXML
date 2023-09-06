#region

using System;
using Sitecore;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.HttpRequest;
using Sitemap.XML.Models;

#endregion

namespace Sitemap.XML.Configuration
{
    public class RobotHandler : HttpRequestProcessor
    {
        public override void Process(HttpRequestArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (Context.Site == null || string.IsNullOrEmpty(Context.Site.RootPath.Trim())) return;
            if (Context.Page.FilePath.Length > 0) return;
            var site = Context.Site;
            if (!args.Url.FilePath.Contains(Constants.RobotsFileName)) return;

            args.HttpContext.Response.ClearHeaders();
            args.HttpContext.Response.ClearContent();
            args.HttpContext.Response.ContentType = "text/plain";

            var content = string.Empty;
            try
            {
                var config = new SitemapManagerConfiguration(site.Name);
                var sitemapManager = new SitemapManager(config);

                content = sitemapManager.GetRobotSite();
                args.HttpContext.Response.Write(content);
            }
            catch (Exception e)
            {
                Log.Error("Error Robots", e, this);
            }
            finally
            {
                args.HttpContext.Response.Flush();
                args.HttpContext.Response.End();
            }
        }
    }
}
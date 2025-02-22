﻿/* *********************************************************************** *
 * File   : SitemapHandler.cs                             Part of Sitecore *
 * Version: 1.0.0                                         www.sitecore.net *
 *                                                                         *
 *                                                                         *
 * Purpose: Contains logic which fires when event submitted                *
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

using System;
using Sitecore.Diagnostics;

namespace Sitemap.XML.Models
{
    public class SitemapHandler
    {
        public void RefreshSitemap(object sender, EventArgs args)
        {
            try
            {
                var sites = SitemapManagerConfiguration.GetSiteNames();

                if (sites == null)
                {
                    Log.Warn("SitemapXML:SitemapHandler.RefreshSitemap:No Sites had been configured at '/sitecore/system/Modules/Sitemap XML'", this);
                }
                else
                {
                    Log.Info("SitemapXML:SitemapHandler.RefreshSitemap: At least one Sitemap Site is found at '/sitecore/system/Modules/Sitemap XML'", this);
                    foreach (var site in sites)
                    {
                        var config = new SitemapManagerConfiguration(site);
                        var sitemapManager = new SitemapManager(config);
                        sitemapManager.SubmitSitemapToSearchenginesByHttp();
                        //removed because now the robots is generated when it is invoked the url with robots.txt at the end
                        if (!config.GenerateRobotsFile) continue;
                        sitemapManager.RegisterSitemapToRobotsFile();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("SitemapXML:",e,this);
            }
        }
    }
}

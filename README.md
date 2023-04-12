What is the Ultimate Sitemap XML Module?
=========================================
The module builds on top of the Sitemap XML module with fixed bugs, updated and extended functionality. The new module is more flexible and exposes many settings via content vs configuration, allowing content editors more control over generating pages.

![Ultimate Sitemap XML Module](http://www.cmsbestpractices.com/wp-content/uploads/2015/07/sitecore-signalr-tools-logo.png)

The module includes the following new features:

- Shared content support
- Custom MVC rendering for displaying a sitemap on a page
- Content controlled sitemap settings
- Page sitemap base template (inherited by page templates and adds sitemap-related meta fields)

Original Sitemap XML features retained:

- Multisite support
- `robots.txt` file generation and updates
- Physical XML sitemap file generation
- `/sitemap.xml` dynamic handler
- Auto sitemap submission to search engines on publish

How to Deploy Ultimate Sitemap XML Module?
-----------------------------------------
**Using the Source**
1. Publish the project directly into the `\Website` folder of the Sitecore instance and Deploy the TDS Master and Core project content.

**Using the Module** 
1. [Download the Sitemap XML module](https://github.com/climax-media/SitecoreSitemapXML/blob/master/Sitemap.XML.Package/SitemapXMLPackages-1.0.zip) 
2. Install the module using the Sitecore Installation Wizard. 
3. Copy `SitemapXML.config` _(/webroot/App_Config/Include/)_ and `SitemapXMl.dll` _(/webroot/bin/)_ files from CM to corresponding location on CD

Installation and Configuration Options
------------------------------------------

1. Install the package using the Update Installation Wizard (`/sitecore/admin/updateinstallationwizard.aspx`)
2. Open the `SitemapXML.config` under the `App_Config\Include` folder. In the `SitemapXML.config` file you can specify the following:
   - `xmlnsTpl` - sitemap module schema used for the XML sitemap.
   - `xmlnsXhtmlTpl` - sitemap xhtml module schema used for the XML sitemap.
   - `database` - the database from which to pull items for generating the sitemap.
   - `sitemapConfigurationItemPath` - root path for the sitemap module configuration settings.
   - `productionEnvironment` - (true or false) determines whether the sitemap should be submitted to the search engines or not.
   - `generateRobotsFile` - (true or false) defines whether a `robots.txt` file should be auto-generated with references to sitemap files or not.
   - `includeLastModInXml` - (true or false) defines whether to include `<lastmod>` in the sitemap xml files or not.

The following are the default values:
```
       <sitemapVariables>
            <sitemapVariable name="xmlnsTpl" value="http://www.sitemaps.org/schemas/sitemap/0.9" />
            <sitemapVariable name="xmlnsXhtmlTpl" value="http://www.w3.org/TR/xhtml11/xhtml11_schema.html" />
            <sitemapVariable name="database" value="web" />
            <sitemapVariable name="sitemapConfigurationItemPath" value="/sitecore/system/Modules/Sitemap XML/" />
            <sitemapVariable name="productionEnvironment" value="false" />
            <sitemapVariable name="generateRobotsFile" value="false" />
            <sitemapVariable name="includeLastModInXml" value="false" />
        </sitemapVariables>
```

4.  Open the Sitecore Content Editor.
5.  Navigate to `/sitecore/system/Modules/Sitemap XML`.
6.  Create a Sitemap Configuration item and name it with the same name as the website name attribute in the `<site />`  definition for your website (Non-case sensitive). 
	
	 This item will contain all of the website-specific configuration used by the module. 
7.  Set which search engines will be used for submitting the XML Sitemap in the Search Engines field(you can add your own search engine by adding the new `Sitemap Search engine` item under the Sitemap XML Search Engines folder and specifying the `Sitemap Submission Url` path.
8.  In the Configuration section of the Sitemap configuration item you can set the following:
    - `Search Engines` - select the search engine to submit the sitemap to
    - `Enabled Templates` - templates to be included in the sitemap
    - `Excluded Items` - individual items to be excluded from the sitemap
    - `File Name` - the name of the sitemap XML file file which will be saved in the root Website directory
    - `Server Url` - the server URL to be used in the sitemap URLs. The module falls back to using the server URL which was used to request the sitemap.
    - `Enabled Languages` - select languages that sitemap URLs muls be inclued with. Leave blank to include all assigned languages.
    - `Use Display Name` - select if Display Name of the item should be used for URL generation. (Selected by default)
    - `Enable Language Embedding` - select if language should be embedded in the URL. (Selected by default)

9.  To include shared content in the sitemap add a `Shared Content Definition` item under the `Sitemap Configuration` item for the website in question and specify the `Parent Item` (parent content item) and `Content Location` (the parent item for the shared content).
10. Sitemap will be generated and submitted to defined search engines after publishing (if `productionEnvironment` setting is set to `true`). Also you can submit sitemap manually. Run the `Sitemap Manager` application (`sitecore menu/all programs/sitemap manager`) and click `Refresh sitemap` button.


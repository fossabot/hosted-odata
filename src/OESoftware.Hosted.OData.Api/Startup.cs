﻿using System;
using System.Net.Http;
using System.Web.Http;
using System.Web.OData.Extensions;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Microsoft.Owin;
using OESoftware.Hosted.OData.Api;
using OESoftware.Hosted.OData.Api.Models;
using OESoftware.Hosted.OData.Api.Routing;
using Owin;

[assembly: OwinStartup(typeof(Startup))]

namespace OESoftware.Hosted.OData.Api
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var webApiConfiguration = ConfigureWebApi();

            // Use the extension method provided by the WebApi.Owin library:
            app.UseWebApi(webApiConfiguration);
        }

        private HttpConfiguration ConfigureWebApi()
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.UseDynamicODataRoute("odata", string.Empty, "", new ModelProvider());
            config.AddODataQueryFilter();

            config.EnsureInitialized();
            return config;
        }
    }
}

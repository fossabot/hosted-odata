﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http;
using System.Web.OData.Batch;
using System.Web.OData.Routing;
using System.Web.OData.Routing.Conventions;
using Microsoft.OData.Edm;

namespace OESoftware.Hosted.OData.Api.Routing
{
    public static class Extensions
    {
        public static void UseDynamicODataRoute(this HttpConfiguration config, string routeName, string routePrefix, string controllerName, Func<HttpRequestMessage, IEdmModel> modelProvider)
        {
            UseDynamicODataRoute(config, routeName, routePrefix, controllerName, modelProvider, null);
        }

        public static void UseDynamicODataRoute(this HttpConfiguration config, string routeName, string routePrefix, string controllerName, Func<HttpRequestMessage, IEdmModel> modelProvider,
            ODataBatchHandler batchHandler)
        {
            if (!string.IsNullOrEmpty(routePrefix))
            {
                var prefixLastIndex = routePrefix.Length - 1;
                if (routePrefix[prefixLastIndex] == '/')
                {
                    routePrefix = routePrefix.Substring(0, routePrefix.Length - 1);
                }
            }

            if (batchHandler != null)
            {
                batchHandler.ODataRouteName = routeName;
                var batchTemplate = string.IsNullOrEmpty(routePrefix)
                    ? ODataRouteConstants.Batch
                    : routePrefix + '/' + ODataRouteConstants.Batch;
                config.Routes.MapHttpBatchRoute(routeName + "Batch", batchTemplate, batchHandler);
            }

            IList<IODataRoutingConvention> routingConventions = ODataRoutingConventions.CreateDefault();
            routingConventions.Insert(0, new DynamicODataRoutingConvention(controllerName));
            DynamicODataPathRouteConstraint routeConstraint = new DynamicODataPathRouteConstraint(
                modelProvider,
                routeName,
                routingConventions);
            var odataRoute = new DynamicODataRoute(routePrefix, routeConstraint);
            config.Routes.Add(routeName, odataRoute);
        }

    }
}
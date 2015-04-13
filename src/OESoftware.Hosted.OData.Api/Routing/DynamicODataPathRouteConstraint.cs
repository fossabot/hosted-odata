﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Routing;
using System.Web.OData;
using System.Web.OData.Extensions;
using System.Web.OData.Routing;
using System.Web.OData.Routing.Conventions;
using Microsoft.OData.Core;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using MongoDB.Bson;
using MongoDB.Driver;
using OESoftware.Hosted.OData.Api.DBHelpers;
using OESoftware.Hosted.OData.Api.Extensions;
using OESoftware.Hosted.OData.Api.Interfaces;

namespace OESoftware.Hosted.OData.Api.Routing
{
    internal class DynamicODataPathRouteConstraint : ODataPathRouteConstraint
    {
        public const string DynamicODataPath = "DynamicODataPath";

        // "%2F"
        private static readonly string EscapedSlash = Uri.HexEscape('/');

        public IModelProvider EdmModelProvider { get; set; }

        public DynamicODataPathRouteConstraint(
            IModelProvider modelProvider,
            string routeName,
            IEnumerable<IODataRoutingConvention> routingConventions)
            : base(new DefaultODataPathHandler(), new EdmModel(), routeName, routingConventions)
        {
            EdmModelProvider = modelProvider;
        }

        public override bool Match(
            HttpRequestMessage request,
            IHttpRoute route,
            string parameterName,
            IDictionary<string, object> values,
            HttpRouteDirection routeDirection)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            if (routeDirection != HttpRouteDirection.UriResolution)
            {
                return true;
            }

            object oDataPathValue;
            if (!values.TryGetValue(ODataRouteConstants.ODataPath, out oDataPathValue))
            {
                return false;
            }

            var oDataPathString = oDataPathValue as string;

            ODataPath path;
            IEdmModel model;
            try
            {
                request.Properties[DynamicODataPath] = oDataPathString;

                model = EdmModelProvider.FromRequest(request).Result;
                oDataPathString = (string)request.Properties[DynamicODataPath];

                var requestLeftPart = request.RequestUri.GetLeftPart(UriPartial.Path);
                var serviceRoot = requestLeftPart;

                if (!String.IsNullOrEmpty(oDataPathString))
                {
                    serviceRoot = RemoveODataPath(serviceRoot, oDataPathString);
                }

                var oDataPathAndQuery = requestLeftPart.Substring(serviceRoot.Length);
                if (!String.IsNullOrEmpty(request.RequestUri.Query))
                {
                    oDataPathAndQuery += request.RequestUri.Query;
                }

                if (serviceRoot.EndsWith(EscapedSlash, StringComparison.OrdinalIgnoreCase))
                {
                    serviceRoot = serviceRoot.Substring(0, serviceRoot.Length - 3);
                }

                path = PathHandler.Parse(model, serviceRoot, oDataPathAndQuery);
            }
            catch (ODataException)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            if (path == null)
            {
                return false;
            }

            //If Path points to a property/operation on a single entity
            //Check that the single entity exists
            if (path.Segments.Count > 2)
            {
                var navProperty = path.Segments[0] as EntitySetPathSegment;
                var collectionType = (IEdmCollectionType) navProperty.EntitySetBase.Type;
                var key = navProperty.EntitySetBase.EntityType().DeclaredKey.FirstOrDefault();
                if (key == null) return false;

                var keyProperty = path.Segments[1] as KeyValuePathSegment;
                var keys = keyProperty.ParseKeyValue(collectionType.ElementType.AsEntity());

                //TODO: Currently do not support multiple keys
                if (keys.Count != 1)
                {
                    return false;
                }

                var dbIdentifier = request.GetOwinEnvironment()["DbId"] as string;
                if (dbIdentifier == null)
                {
                    throw new ApplicationException("Invalid DB identifier");
                }

                var dbConnection = DBConnectionFactory.Open(dbIdentifier);
                var collection = dbConnection.GetCollection<BsonDocument>(collectionType.FullTypeName());

                var existing = collection.FindAsync(
                    new BsonDocumentFilterDefinition<BsonDocument>(
                        new BsonDocument(new BsonElement("_id", BsonValue.Create(keys.Values.First())))), new FindOptions<BsonDocument>() {Limit = 1}).Result.ToListAsync().Result.FirstOrDefault();

                if (existing == null)
                {
                    return false;
                }

                var entity = new EdmEntityObject(collectionType.ElementType.AsEntity());
                foreach (var element in existing.Elements)
                {
                    var name = element.Name;
                    if (name.Equals("_id"))
                    {
                        name = keys.Keys.First();
                    }
                    entity.TrySetPropertyValue(name, BsonTypeMapper.MapToDotNetValue(element.Value));
                }

                request.Properties.Add("BaseEntity", entity);
                request.Properties.Add("BaseEntityKeys", keys);
            }

            var odataProperties = request.ODataProperties();
            odataProperties.Model = model;
            odataProperties.PathHandler = PathHandler;
            odataProperties.Path = path;
            odataProperties.RouteName = RouteName;
            odataProperties.RoutingConventions = RoutingConventions;

            if (values.ContainsKey(ODataRouteConstants.Controller))
            {
                return true;
            }

            var controllerName = SelectControllerName(path, request);
            if (controllerName != null)
            {
                values[ODataRouteConstants.Controller] = controllerName;
            }

            return true;
        }

        private static string RemoveODataPath(string uriString, string oDataPathString)
        {
            // Potential index of oDataPathString within uriString.
            var endIndex = uriString.Length - oDataPathString.Length - 1;
            if (endIndex <= 0)
            {
                // Bizarre: oDataPathString is longer than uriString.  Likely the values collection passed to Match()
                // is corrupt.
                throw new InvalidOperationException(string.Format("Request Uri Is Too Short For ODataPath. the Uri is {0}, and the OData path is {1}.", uriString, oDataPathString));
            }

            var startString = uriString.Substring(0, endIndex + 1);  // Potential return value.
            var endString = uriString.Substring(endIndex + 1);       // Potential oDataPathString match.
            if (String.Equals(endString, oDataPathString, StringComparison.Ordinal))
            {
                // Simple case, no escaping in the ODataPathString portion of the Path.  In this case, don't do extra
                // work to look for trailing '/' in startString.
                return startString;
            }

            while (true)
            {
                // Escaped '/' is a derivative case but certainly possible.
                var slashIndex = startString.LastIndexOf('/', endIndex - 1);
                var escapedSlashIndex =
                    startString.LastIndexOf(EscapedSlash, endIndex - 1, StringComparison.OrdinalIgnoreCase);
                if (slashIndex > escapedSlashIndex)
                {
                    endIndex = slashIndex;
                }
                else if (escapedSlashIndex >= 0)
                {
                    // Include the escaped '/' (three characters) in the potential return value.
                    endIndex = escapedSlashIndex + 2;
                }
                else
                {
                    // Failure, unable to find the expected '/' or escaped '/' separator.
                    throw new InvalidOperationException(string.Format("The OData path is not found. The Uri is {0}, and the OData path is {1}.", uriString, oDataPathString));
                }

                startString = uriString.Substring(0, endIndex + 1);
                endString = uriString.Substring(endIndex + 1);

                // Compare unescaped strings to avoid both arbitrary escaping and use of lowercase 'a' through 'f' in
                // %-escape sequences.
                endString = Uri.UnescapeDataString(endString);
                if (String.Equals(endString, oDataPathString, StringComparison.Ordinal))
                {
                    return startString;
                }

                if (endIndex == 0)
                {
                    // Failure, could not match oDataPathString after an initial '/' or escaped '/'.
                    throw new InvalidOperationException(string.Format("The OData path is not found. The Uri is {0}, and the OData path is {1}.", uriString, oDataPathString));
                }
            }
        }
    }
}
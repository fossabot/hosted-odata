﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Library;
using Microsoft.OData.Edm.Library.Annotations;
using Microsoft.OData.Edm.Validation;
using MongoDB.Bson;
using MongoDB.Driver;
using OESoftware.Hosted.OData.Api.DBHelpers;
using OESoftware.Hosted.OData.Api.Interfaces;

namespace OESoftware.Hosted.OData.Api.Models
{
    public class ModelProvider : IModelProvider
    {
        private const string DynamicODataPath = "DynamicODataPath";
        private const string ODataDataSource = "ODataDataSource";
        private static readonly MemoryCache ModelCache = new MemoryCache("ModelCache");

        public async Task<IEdmModel> FromRequest(HttpRequestMessage request)
        {
            string odataPath = request.Properties[DynamicODataPath] as string ?? string.Empty;
            string[] segments = odataPath.Split('/');
            string dataSource = segments[0];
            request.Properties[ODataDataSource] = dataSource;
            request.Properties[DynamicODataPath] = string.Join("/", segments, 1, segments.Length - 1);

            var dbIdentifier = request.GetOwinEnvironment()["DbId"] as string;
            if (dbIdentifier == null)
            {
                throw new ApplicationException("Invalid DB identifier");
            }

            var cached = ModelCache.Get(dbIdentifier) as IEdmModel;
            if (cached != null)
            {
                return cached;
            }

            var dbConnection = DBConnectionFactory.Open(dbIdentifier); //TODO: Get db name;
            var collection = dbConnection.GetCollection<SchemaElement>("_schema");

            var allSchemaElements = await collection.FindAsync(new BsonDocument());
            var readers = new List<XmlReader>();
            var disposables = new List<IDisposable>();
            EdmEntityContainer entityContainer = null;
            try
            {
                IEdmModel model;
                IEnumerable<EdmError> errors;
                await
                    allSchemaElements.ForEachAsync(f =>
                    {
                        var stringReader = new StringReader(f.Csdl);
                        var xmlReader = XmlReader.Create(stringReader);
                        readers.Add(xmlReader);
                        disposables.Add(xmlReader);
                        disposables.Add(stringReader);
                        if (entityContainer == null)
                        {
                            entityContainer = new EdmEntityContainer(f.ContainerNamespace, f.ContainerName);
                        }
                    });
                CsdlReader.TryParse(readers, out model, out errors);
                var edmModel = model as EdmModel;
                if (edmModel != null)
                {
                    edmModel.AddElement(entityContainer);

                    ModelCache.Add(dbIdentifier, model, new CacheItemPolicy());
                }

                return model ?? new EdmModel();
            }
            catch
            {
                return new EdmModel();
            }
            finally
            {
                disposables.ForEach(r => r.Dispose());
            }
        }

        public async Task<bool> SaveModel(IEdmModel model, HttpRequestMessage request, bool replace)
        {
            var updates = model.ToDbUpdates();
            if (updates.ModelErrors.Any()) return false;

            var dbIdentifier = request.GetOwinEnvironment()["DbId"] as string;
            if (dbIdentifier == null)
            {
                throw new ApplicationException("Invalid DB identifier");
            }

            var dbConnection = DBConnectionFactory.Open(dbIdentifier); //TODO: Get db name;
            var collection = dbConnection.GetCollection<SchemaElement>("_schema");

            if (replace)
            {
                await collection.DeleteManyAsync(Builders<SchemaElement>.Filter.In(f => f.Namespace, model.DeclaredNamespaces));
            }

            var updateOperations = updates.Updates.Select(update => new UpdateOneModel<SchemaElement>(update.FilterDefinition, update.UpdateDefinition) {IsUpsert = true}).ToList();

            await collection.BulkWriteAsync(updateOperations);

            ModelCache.Remove(dbIdentifier);

            return true;
        }

        public async Task<bool> DeleteModel(IEdmModel model, HttpRequestMessage request)
        {
            var updates = model.ToDbUpdates();
            if (updates.ModelErrors.Any()) return false;

            var dbIdentifier = request.GetOwinEnvironment()["DbId"] as string;
            if (dbIdentifier == null)
            {
                throw new ApplicationException("Invalid DB identifier");
            }

            var dbConnection = DBConnectionFactory.Open(dbIdentifier); //TODO: Get db name;
            var collection = dbConnection.GetCollection<SchemaElement>("_schema");

            var deleteOperations = model.SchemaElements.Select(schema => new DeleteOneModel<SchemaElement>(new ExpressionFilterDefinition<SchemaElement>(s => s.Name == schema.Name && s.Namespace == schema.Namespace))).ToList();

            await collection.BulkWriteAsync(deleteOperations);

            ModelCache.Remove(dbIdentifier);

            return true;
        }

        public IEdmModel FromXml(string xml, out IEnumerable<EdmError> errors)
        {
            var readers = new List<XmlReader>();
            var disposables = new List<IDisposable>();
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                doc.GetElementsByTagName("Schema").Cast<XmlNode>().ToList().ForEach(node =>
                {
                    var stringReader = new StringReader(node.OuterXml);
                    var xmlReader = XmlReader.Create(stringReader);
                    readers.Add(xmlReader);
                    disposables.Add(xmlReader);
                    disposables.Add(stringReader);
                });

                IEdmModel model;
                CsdlReader.TryParse(readers.AsEnumerable(), out model, out errors);

                return model;
            }
            finally
            {
                disposables.ForEach(r => r.Dispose());
            }
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace OESoftware.Hosted.OData.Api.DBHelpers.Commands
{
    public class DbUpdateCommand<T> : IDbCommand<T>
    {
        public string CollectionName { get; private set; }

        public UpdateDefinition<T> UpdateDefinition { get; private set; }

        public FilterDefinition<T> FilterDefinition { get; private set; }

        public DbUpdateCommand(string collectionName, FilterDefinition<T> filterDefinition, UpdateDefinition<T> updateBuilder)
        {
            CollectionName = collectionName;
            UpdateDefinition = updateBuilder;
            FilterDefinition = filterDefinition;
        }

        public async Task Execute(IMongoCollection<T> collection)
        {
            await collection.FindOneAndUpdateAsync(FilterDefinition, UpdateDefinition);
        }
    }
}

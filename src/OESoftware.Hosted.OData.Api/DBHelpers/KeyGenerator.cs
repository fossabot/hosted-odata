﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using MongoDB.Bson;
using MongoDB.Driver;
using OESoftware.Hosted.OData.Api.Models;

namespace OESoftware.Hosted.OData.Api.DBHelpers
{
    public class KeyGenerator
    {
        public async Task<Int16> CreateInt16Key(HttpRequestMessage request, string keyName)
        {
            return (Int16)(await GetNextFromCounters(request, keyName, (Int16) 1));
        }

        public async Task<Int32> CreateInt32Key(HttpRequestMessage request, string keyName)
        {
            return (Int32)(await GetNextFromCounters(request, keyName, (Int32)1));
        }

        public async Task<Int64> CreateInt64Key(HttpRequestMessage request, string keyName)
        {
            return (Int64)(await GetNextFromCounters(request, keyName, (Int64)1));
        }

        public async Task<Decimal> CreateDecimalKey(HttpRequestMessage request, string keyName)
        {
            return (Decimal)(await GetNextFromCounters(request, keyName, (Decimal)1.0));
        }

        public async Task<Double> CreateDoubleKey(HttpRequestMessage request, string keyName)
        {
            return (Double)(await GetNextFromCounters(request, keyName, (Double)1.0));
        }

        public async Task<Guid> CreateGuidKey(HttpRequestMessage request, string keyName)
        {
            return Guid.NewGuid();
        }

        public async Task<Single> CreateSingleKey(HttpRequestMessage request, string keyName)
        {
            return (Single)(await GetNextFromCounters(request, keyName, (Single)1.0));
        }

        private async Task<object> GetNextFromCounters(HttpRequestMessage request, string keyName, object increment)
        {
            var dbIdentifier = request.GetOwinEnvironment()["DbId"] as string;
            if (dbIdentifier == null)
            {
                throw new ApplicationException("Invalid DB identifier");
            }

            var dbConnection = DBConnectionFactory.Open(dbIdentifier);
            var collection = dbConnection.GetCollection<IdCounter>("_counters");

            var filter = new ExpressionFilterDefinition<IdCounter>(f => f.Name == keyName);
            var update = Builders<IdCounter>.Update
                .Inc(f=>f.Counter, increment);

            var counter = await collection.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<IdCounter>() {IsUpsert = true, ReturnDocument = ReturnDocument.After});

            return counter.Counter;
        }
    }
}
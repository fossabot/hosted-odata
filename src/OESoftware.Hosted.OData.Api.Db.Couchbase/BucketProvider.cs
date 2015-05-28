﻿// Copyright (C) 2015 Michael Norgate

// This software may be modified and distributed under the terms of 
// the Creative Commons Attribution Non-commercial license.  See the LICENSE file for details.

#region usings

using Couchbase;
using Couchbase.Core;

#endregion

namespace OESoftware.Hosted.OData.Api.Db.Couchbase
{
    /// <summary>
    ///     Get a bucket from the Couchbase cluster
    /// </summary>
    public static class BucketProvider
    {
        private static readonly Cluster Cluster = new Cluster("couchbaseClients/couchbase");

        /// <summary>
        ///     Get the default bucket
        /// </summary>
        /// <returns>
        ///     <see cref="IBucket" />
        /// </returns>
        public static IBucket GetBucket()
        {
            return Cluster.OpenBucket();
        }

        /// <summary>
        ///     Get bucket by name
        /// </summary>
        /// <param name="name">Name of bucket</param>
        /// <returns>
        ///     <see cref="IBucket" />
        /// </returns>
        public static IBucket GetBucket(string name)
        {
            return Cluster.OpenBucket(name);
        }
    }
}
using System;
using System.Collections.Generic;
using Google.Protobuf;
using pb=KubeMQ.Grpc ;
namespace KubeMQ.SDK.csharp.CQ.Queries
{
    public class Query
    {
        /// <summary>
        /// The ID of the query message.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The channel through which the query message will be sent.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Additional metadata associated with the query message.
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// The body of the query message as bytes.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// A dictionary of key-value pairs representing tags associated with the query message.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; }

        /// <summary>
        /// The maximum time in seconds for which the query message is valid.
        /// </summary>
        public int TimeoutInSeconds { get; set; }
        
        /// <summary>
        /// Represents if the request should be saved from Cache and under what "Key"(System.String) to save it.
        /// </summary>
        public string CacheKey { get; set; }
        
        /// <summary>
        /// Represents The time to live for the request in Cache (Milliseconds)
        /// </summary>
        public int CacheTTL { get; set; }

        /// <summary>
        /// Initializes a new instance of the KubeMQ.SDK.csharp.RequestReply.Request for KubeMQ.SDK.csharp.RequestReply.channel use.
        /// </summary>

        public Query()
        {
            
        }
        public Query(string id = null, string channel = null, string metadata = null, byte[] body = null, Dictionary<string, string> tags = null, int timeoutInSeconds = 0, string cacheKey = null, int cacheTTL = 0)
        {
            Id = id;
            Channel = channel;
            Metadata = metadata;
            Body = body ?? Array.Empty<byte>();
            Tags = tags ?? new Dictionary<string, string>();
            TimeoutInSeconds = timeoutInSeconds;
            CacheKey = cacheKey;
            CacheTTL = cacheTTL;
        }
        
        
        /// <summary>
        /// Sets the identifier of the query message.
        /// </summary>
        /// <param name="id">The identifier to set.</param>
        /// <returns>The <see cref="Query"/> instance with the modified identifier.</returns>
        public Query SetId(string id)
        {
            Id = id;
            return this;
        }

        /// <summary>
        /// Sets the channel for the Query message.
        /// </summary>
        /// <param name="channel">The channel to set for the Query message.</param>
        /// <returns>The updated Query instance.</returns>
        public Query SetChannel(string channel)
        {
            Channel = channel;
            return this;
        }

        /// <summary>
        /// Sets the metadata value for the query.
        /// </summary>
        /// <param name="metadata">The metadata value for the query.</param>
        /// <returns>The updated <see cref="Query"/> object.</returns>
        public Query SetMetadata(string metadata)
        {
            Metadata = metadata;
            return this;
        }

        /// <summary>
        /// Sets the body of the query message.
        /// </summary>
        /// <param name="body">The byte array representing the body of the query.</param>
        /// <returns>The updated <see cref="Query"/> instance.</returns>
        public Query SetBody(byte[] body)
        {
            Body = body;
            return this;
        }

        /// <summary>
        /// Sets the tags for the query message.
        /// </summary>
        /// <param name="tags">The dictionary of key-value pairs representing the tags.</param>
        /// <returns>The <see cref="Query"/> instance with the updated tags.</returns>
        public Query SetTags(Dictionary<string, string> tags)
        {
            Tags = tags ?? new Dictionary<string, string>();
            return this;
        }

        /// <summary>
        /// Sets the timeout for the query message.
        /// </summary>
        /// <param name="timeoutInSeconds">The timeout duration in seconds.</param>
        /// <returns>The updated <see cref="Query"/> instance.</returns>
        public Query SetTimeout(int timeoutInSeconds)
        {
            TimeoutInSeconds = timeoutInSeconds;
            return this;
        }


        /// <summary>
        /// Sets the cache key for the query.
        /// </summary>
        /// <param name="cacheKey">The cache key to set for the query.</param>
        /// <returns>The updated <see cref="Query"/> object.</returns>
        public Query SetCacheKey(string cacheKey)
        {
            CacheKey = cacheKey;
            return this;
        }

        /// <summary>
        /// Sets the time-to-live (TTL) value for the query cache.
        /// </summary>
        /// <param name="cacheTTL">The time-to-live (TTL) value to set for the query cache.</param>
        /// <returns>The updated Query instance.</returns>
        public Query SetCacheTTL(int cacheTTL)
        {
            CacheTTL = cacheTTL;
            return this;
        }
        
        /// <summary>
        /// Validates the query message. Throws a <see cref="ArgumentException"/> if the message is invalid.
        /// </summary>
        /// <returns>The validated <see cref="Query"/> instance.</returns>
        internal Query Validate()
        {
            if (string.IsNullOrEmpty(Channel))
                throw new ArgumentException("Query message must have a channel.");

            if (string.IsNullOrEmpty(Metadata) && Body.Length == 0 && Tags.Count == 0)
                throw new ArgumentException("Query message must have at least one of the following: metadata, body, or tags.");

            if (TimeoutInSeconds <= 0)
                throw new ArgumentException("Query message timeout must be a positive integer.");

            return this;
        }

        /// <summary>
        /// Encodes the query message into a protocol buffer message.
        /// </summary>
        /// <param name="clientId">The ID of the client.</param>
        /// <returns>The encoded message.</returns>
        internal pb.Request Encode(string clientId)
        {
            var pbQuery = new pb.Request()
            {
                RequestID = Id ?? Guid.NewGuid().ToString(),
                ClientID = clientId,
                Channel = Channel,
                Metadata = Metadata ?? string.Empty,
                Body = ByteString.CopyFrom(Body),
                Timeout = TimeoutInSeconds * 1000,
                RequestTypeData = pb.Request.Types.RequestType.Query,
                CacheTTL = CacheTTL * 1000,
                CacheKey = CacheKey
            };

            foreach (var kvp in Tags)
                pbQuery.Tags.Add(kvp.Key, kvp.Value);

            return pbQuery;
        }

        public override string ToString()
        {
            return $"QueryMessage: id={Id}, channel={Channel}, metadata={Metadata}, body={Body}, tags={string.Join(",", Tags)}, timeout_in_seconds={TimeoutInSeconds}, cache_key={CacheKey}, cache_ttl={CacheTTL}";
        }
    }
}
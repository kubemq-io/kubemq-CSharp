using System;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using static KubeMQ.Grpc.kubemq;
using System.IO;

namespace KubeMQ.SDK.csharp.Basic {
    public class GrpcClient {
        protected string _kubemqAddress;
        protected Metadata _metadata = null;
        protected kubemqClient _client = null;

        public string ServerAddress {
            get {
                return GetKubeMQAddress ();
            }
            private set {
                _kubemqAddress = value;
            }
        }

        public Metadata Metadata { 
            get 
            { 
                return _metadata;
            }
        }

        protected kubemqClient GetKubeMQClient () {
            if (_client != null) {
                return _client;
            }

            return CreateNewClient ();
        }

        protected void addAuthToken (string authToken) {
            if (authToken == null) {
                return;
            }

            if (this._metadata == null) {
                this._metadata = new Metadata ();
            }
            this._metadata.Add(new Metadata.Entry ("authorization", authToken));
        }

        private kubemqClient CreateNewClient () {
            Channel channel;
            string kubemqAddress = GetKubeMQAddress ();
            string clientCertFile = ConfigurationLoader.GetCertificateFile ();

            if (!string.IsNullOrWhiteSpace (clientCertFile)) {
                // Open SSL/TLS connection 
                var channelCredentials = new SslCredentials (File.ReadAllText (clientCertFile));
                channel = new Channel (kubemqAddress, channelCredentials);
            } else {
                // Open Insecure connection
                channel = new Channel (kubemqAddress, ChannelCredentials.Insecure);
            }

            _client = new kubemqClient (channel);
            

            //logger.LogTrace("Opened connection to KubeMQ server (ip:port) {0}", kubemqAddress);

            return _client;
        }

        private string GetKubeMQAddress () {
            if (!string.IsNullOrWhiteSpace (_kubemqAddress)) // _kubemqAddress was supplied in the derived constructor
                return _kubemqAddress;

            _kubemqAddress = ConfigurationLoader.GetServerAddress ();

            if (string.IsNullOrWhiteSpace (_kubemqAddress)) {
                throw new Exception ("Server Address was not supplied");
            }

            return _kubemqAddress;
        }

    }
}
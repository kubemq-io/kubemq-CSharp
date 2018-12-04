using Grpc.Core;
using System;
using Microsoft.Extensions.Logging;
using static KubeMQ.Grpc.kubemq;
using System.IO;

namespace KubeMQ.SDK.csharp.Basic
{
    public class GrpcClient
    {
        protected string _kubemqAddress;
        protected Metadata _metadata = null;
        protected kubemqClient _client = null;

        public GrpcClient()
        {
            InitRegistration();
        }

        public string ServerAddress
        {
            get
            {
                return GetKubeMQAddress();
            }
            private set
            {
                _kubemqAddress = value;
            }
        }

        protected kubemqClient GetKubeMQClient()
        {
            if (_client != null)
            {
                return _client;
            }

            return CreateNewClient();
        }

        private kubemqClient CreateNewClient()
        {
            Channel channel;
            string kubemqAddress = GetKubeMQAddress();
            string clientCertFile = ConfigurationLoader.GetCertificateFile();

            if (!string.IsNullOrWhiteSpace(clientCertFile))
            {
                // Open SSL/TLS connection 
                var channelCredentials = new SslCredentials(File.ReadAllText(clientCertFile));
                channel = new Channel(kubemqAddress, channelCredentials);
            }
            else
            {
                // Open Insecure connection
                channel = new Channel(kubemqAddress, ChannelCredentials.Insecure);
            }

            _client = new kubemqClient(channel);


            //logger.LogTrace("Opened connection to KubeMQ server (ip:port) {0}", kubemqAddress);

            return _client;
        }

        private string GetKubeMQAddress()
        {
            if (!string.IsNullOrWhiteSpace(_kubemqAddress))// _kubemqAddress was supplied in the derived constructor
                return _kubemqAddress;

            _kubemqAddress = ConfigurationLoader.GetServerAddress();

            if (string.IsNullOrWhiteSpace(_kubemqAddress))
            {
                throw new Exception("Server Address was not supplied");
            }

            return _kubemqAddress;
        }

        private void InitRegistration()
        {
            string registrationKey = ConfigurationLoader.GetRegistrationKey();

            if (!string.IsNullOrWhiteSpace(registrationKey))
            {
                _metadata = new Metadata { { "X-Kubemq-Server-Token", registrationKey } };
            }
        }
    }
}

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Specialized;
using System.IO;

namespace KubeMQ.SDK.csharp.Basic
{
    internal class ConfigurationLoader
    {
        private static string _path = null; // ServerAddress
        private static string _key = null;  // RegistrationKey
        private static string _cert = null;  // Certificate file.

        internal static string GetServerAddress()
        {
            if (!string.IsNullOrWhiteSpace(_path)) return _path;

            _path = GetFromEnvironmentVariable("KUBEMQSERVERADDRESS");

            if (!string.IsNullOrWhiteSpace(_path)) return _path;

            _path = GetFromJson("KubeMQ:serverAddress");

            if (!string.IsNullOrWhiteSpace(_path)) return _path;

            _path = GetFromAppConfig("serverAddress");

            return _path;
        }

        internal static string GetRegistrationKey()//KubeMQLicenseKey
        {
            if (_key != null) return _key;

            _key = GetFromEnvironmentVariable("KubeMQRegistrationKey");

            if (!string.IsNullOrWhiteSpace(_key)) return _key;

            _key = GetFromJson("KubeMQ:registrationKey");

            if (!string.IsNullOrWhiteSpace(_key)) return _key;

            _key = GetFromAppConfig("registrationKey");

            if (string.IsNullOrWhiteSpace(_key))
            {
                _key = string.Empty;
            }

            return _key;
        }

        internal static string GetCertificateFile()
        {
            if (_cert != null) return _cert;

            _cert = GetFromEnvironmentVariable("KubeMQCertificateFile");

            if (!string.IsNullOrWhiteSpace(_cert)) return _cert;

            _cert = GetFromJson("KubeMQ:certificateFile");

            if (!string.IsNullOrWhiteSpace(_cert)) return _cert;

            _cert = GetFromAppConfig("certificateFile");

            if (string.IsNullOrWhiteSpace(_cert))
            {
                _cert = string.Empty;
            }

            return _cert;
        }

        private static string GetFromEnvironmentVariable(string key)
        {
            string serverAddress = null;

            for (int target = 0; target < 3; target++)// look in all location where an environment variable is stored 
            {
                serverAddress = Environment.GetEnvironmentVariable(key.ToUpper(), (EnvironmentVariableTarget)target);// returns null if not found.

                if (!string.IsNullOrWhiteSpace(serverAddress))
                {
                    break;
                }
            }
            
            return serverAddress;
        }

        private static string GetFromJson(string key)
        {
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                     .AddJsonFile("appsettings.json", true);
            var configuration = builder.Build();


            string serverAddress = configuration[key];// returns null if not found.

            return serverAddress;
        }

        private static string GetFromAppConfig(string key)
        {
            string serverAddress = null;

            var KubeMQSettings = System.Configuration.ConfigurationManager.GetSection("KubeMQ") as NameValueCollection;
            if (KubeMQSettings?.Count > 0)
            {
                serverAddress = KubeMQSettings[key];
            }

            return serverAddress;
        }
    }
}

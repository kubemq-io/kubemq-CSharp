using System;
using System.IO;

namespace KubeMQ.SDK.csharp.Config
{
    public class TlsConfig
    {
        public bool Enabled { get; private set; } = false;
        public string CertFile { get; private set; } = "";
        public string KeyFile { get; private set; } = "";
        public string CaFile { get; private set; } = "";

        public TlsConfig SetEnabled(bool enabled)
        {
            Enabled = enabled;
            return this;
        }

        public TlsConfig SetCertFile(string cert)
        {
            CertFile = cert;
            return this;
        }

        public TlsConfig SetKeyFile(string key)
        {
            KeyFile = key;
            return this;
        }

        public TlsConfig SetCaFile(string ca)
        {
            CaFile = ca;
            return this;
        }

        public void Validate()
        {
            if (Enabled)
            {
                if (!string.IsNullOrEmpty(CertFile) && !File.Exists(CertFile))
                {
                    throw new FileNotFoundException($"The certificate file was not found: {CertFile}");
                }

                if (!string.IsNullOrEmpty(KeyFile) && !File.Exists(KeyFile))
                {
                    throw new FileNotFoundException($"The key file was not found: {KeyFile}");
                }

                if (!string.IsNullOrEmpty(CaFile) && !File.Exists(CaFile))
                {
                    throw new FileNotFoundException($"The CA file was not found: {CaFile}");
                }
            }
        }
    }
    
}
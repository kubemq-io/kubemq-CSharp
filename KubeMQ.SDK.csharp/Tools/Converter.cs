using Google.Protobuf;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace KubeMQ.SDK.csharp.Tools
{
    /// <summary>
    /// A class that is responsible for Converting Byte[] to object and vice versa.
    /// </summary>
    public class Converter
    {

        /// <summary>
        /// Convert from string to byte array
        /// </summary>
        public static byte[]ToUTF8(string data)
        {
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(data);
            return utf8Bytes;
        }

        /// <summary>
        /// Convert from byte array to string
        /// </summary>
        public static string FromUTF8(byte[] data)
        {
            string utf8String = System.Text.Encoding.UTF8.GetString(data);
            return utf8String;
        }

        /// <summary>
        /// Byte Array to ByteString
        /// </summary>
        internal static ByteString ToByteString(byte[] byteArray)
        {
            return ByteString.CopyFrom(byteArray);
        }

        /// <summary>
        /// Convert from byte array to object
        /// </summary>
        public static object FromByteArray(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(data))
            {
                object obj = bf.Deserialize(ms);
                return obj;
            }
        }

        /// <summary>
        /// Convert from object to byte array
        /// </summary>
        public static byte[] ToByteArray(object obj)
        {
            if (obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        internal static DateTime FromUnixTime(long UnixTime)
        {
            var timeSpan = TimeSpan.FromSeconds(UnixTime);
            return new DateTime(timeSpan.Ticks).ToLocalTime();
        }

        internal static long ToUnixTime(DateTime timestamp)
        {       
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(timestamp.ToUniversalTime() - epoch).TotalSeconds;
        }


        internal static Google.Protobuf.Collections.MapField<string, string> CreateTags(System.Collections.Generic.Dictionary<string, string> tags)
        {
            Google.Protobuf.Collections.MapField<string, string> keyValuePairs = new Google.Protobuf.Collections.MapField<string, string>();
            if (tags != null)
            {
                foreach (var item in tags)
                {
                    keyValuePairs.Add(item.Key, item.Value);
                }
            }
            return keyValuePairs;
        }

        public static System.Collections.Generic.Dictionary<string, string> ReadTags(Google.Protobuf.Collections.MapField<string, string> tags)
        {
            System.Collections.Generic.Dictionary<string, string> keyValuePairs = new System.Collections.Generic.Dictionary<string, string>();
            if (tags != null)
            {
                foreach (var item in tags)
                {
                    keyValuePairs.Add(item.Key, item.Value);
                }
            }
            return keyValuePairs;
        }

    }
}

using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace KubeMQ.Utils
{
    public static class Convertors
    {
        public static ByteString FromByteArray(byte[] array)
        {
            return array == null ? ByteString.Empty : ByteString.CopyFrom(array);
        }
        public static ByteString FromUtf8String(string value)
        {
            return string.IsNullOrEmpty(value) ? ByteString.Empty : ByteString.CopyFromUtf8(value);
        }
        public static ByteString FromBase64String(string value)
        {
            return string.IsNullOrEmpty(value) ? ByteString.Empty : ByteString.FromBase64(value);
        }
        public static ByteString FromStringWithEncoding(string value, Encoding encoding)
        {
            return string.IsNullOrEmpty(value) ? ByteString.Empty : ByteString.CopyFrom(value,encoding);
        }
        
        public static byte[] ToByteArray(ByteString value)
        {
            return value==null ? null : value.ToByteArray();
        }
        public static byte[] ToByteArray(string value)
        {
            return string.IsNullOrEmpty(value) ? null : Encoding.Default.GetBytes(value);
        }
        public static string ToString(ByteString value)
        {
            return value==null ? "" : value.ToString();
        }
        public static string ToString(byte[] array)
        {
            return array==null ? "" :  Encoding.Default.GetString(array);
        }
        public static string ToUtf8String(ByteString value)
        {
            return value==null ? "" : value.ToStringUtf8();
        }
        public static string ToBase64(ByteString value)
        {
            return value==null ? "" : value.ToBase64();
        }
        public  static MapField<string, string> ToMapFields(Dictionary<string, string> tags)
        {
            MapField<string, string> keyValuePairs = new MapField<string, string>();
            if (tags != null)
            {
                foreach (var item in tags)
                {
                    keyValuePairs.Add(item.Key, item.Value);
                }
            }
            return keyValuePairs;
        }
        public static Dictionary<string, string> FromMapFields(MapField<string, string> tags)
        {
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
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
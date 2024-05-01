using Google.Protobuf;
using Google.Protobuf.Collections;
using KubeMQ.Grpc;
using KubeMQ.SDK.csharp.Queue;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

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
        /// Convert from string to byte array
        /// </summary>
        public static byte[]FromString(string data)
        {
            byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(data);
            return utf8Bytes;
        }

        /// <summary>
        /// Convert from byte array to string
        /// </summary>
        public static string ToString(byte[] data)
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
        //
        //
        // /// <summary>
        // /// Convert from byte array to object in  BinaryFormatter
        // /// </summary>
        // /// <param name="data">Byte Array</param>
        // /// <returns>object</returns>
        // public static object FromByteArray(byte[] data)
        // {
        //     if (data == null || data.Length == 0)
        //         return null;
        //     BinaryFormatter bf = new BinaryFormatter();
        //     using (MemoryStream ms = new MemoryStream(data))
        //     {
        //         object obj = bf.Deserialize(ms);
        //         return obj;
        //     }
        // }
        //

        /// <summary>
        /// Convert from object to byte array in selected  IFormatter
        /// </summary>
        /// <param name="data">Byte Array</param>
        /// <param name="formatter">IFormatter type</param>
        /// <returns>byte array</returns>
        // public static object FromByteArray(byte[] data, IFormatter formatter)
        // {
        //     if (data == null || data.Length == 0)
        //         return null;
        //     using (MemoryStream ms = new MemoryStream(data))
        //     {
        //         object obj = formatter.Deserialize(ms);
        //         return obj;
        //     }
        // }

        // /// <summary>
        // /// Convert from object to byte array in  BinaryFormatter
        // /// </summary>
        // /// <param name="obj">Object to format </param>
        // /// <returns>ByteArray</returns>
        // public static byte[] ToByteArray(object obj)
        // {
        //     if (obj == null)
        //         return null;
        //     BinaryFormatter bf = new BinaryFormatter();
        //     using (MemoryStream ms = new MemoryStream())
        //     {
        //         bf.Serialize(ms, obj);
        //         return ms.ToArray();
        //     }
        // }
        //
        // /// <summary>
        // /// Convert from object to byte array in selected  IFormatter
        // /// </summary>
        // /// <param name="obj">Object to return as bytearray </param>
        // /// <param name="formatter">IFormatter type</param>
        // /// <returns>ByteArray</returns>
        // public static byte[] ToByteArray(object obj, IFormatter formatter)
        // {
        //     if (obj == null)
        //         return null;
        //     using (MemoryStream ms = new MemoryStream())
        //     {
        //         formatter.Serialize(ms, obj);
        //         return ms.ToArray();
        //     }
        // }

        public static DateTime FromUnixTime(long UnixTime)
        {
            double UnixTimeDbl = UnixTime;
            var len = UnixTimeDbl.ToString("F0").Length;
            if (len > 10)
            {
                UnixTimeDbl = UnixTimeDbl / Math.Pow(10, len - 10);
            }
            // UnixTime = 1566126695;
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            return dtDateTime.AddSeconds(UnixTimeDbl).ToLocalTime();
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

        internal static System.Collections.Generic.Dictionary<string, string> ReadTags(Google.Protobuf.Collections.MapField<string, string> tags)
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

        #region "Stream Transaction"
        internal static IEnumerable<Message> FromQueueMessages(RepeatedField<QueueMessage> messages)
        {
            List<Message> msgs = new List<Message>();
            foreach (var item in messages)
            {
                msgs.Add(new Message(item));
            }
            return msgs;
        }


        internal static IEnumerable<QueueMessage> ToQueueMessages(IEnumerable<Message> queueMessages, KubeMQ.SDK.csharp.Queue.Queue queue)
        {
            foreach (var item in queueMessages)
            {
                item.Queue = item.Queue?? queue.QueueName;
                item.ClientID = item.ClientID ?? queue.ClientID;                
                yield return ConvertQueueMessage(item);
            }
        }

        internal static QueueMessage ConvertQueueMessage(Message r)
        {
            return new QueueMessage
            {
                Attributes = r.Attributes,
                Body = Google.Protobuf.ByteString.CopyFrom(r.Body),
                Channel = r.Queue,
                ClientID = r.ClientID,
                MessageID = r.MessageID,
                Metadata = r.Metadata,
                Policy = r.Policy,
                Tags = { Tools.Converter.CreateTags(r.Tags) }
            };
        }

        #endregion

    }
    public static class JsonConverter
    {
        // Converts an object to a byte array using JSON serialization
        public static byte[] ToByteArray<T>(T obj)
        {
            if (obj == null)
                return null;
        
            // Serialize the object to a JSON string, then get the bytes
            string jsonString = JsonConvert.SerializeObject(obj);
            return Encoding.UTF8.GetBytes(jsonString);
        }

        // Converts a byte array back to an object of type T using JSON deserialization
        public static T FromByteArray<T>(byte[] data)
        {
            if (data == null || data.Length == 0)
                return default(T);
        
            // Deserialize the JSON bytes back to an object of type T
            string jsonString = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<T>(jsonString);
        }
    }
}
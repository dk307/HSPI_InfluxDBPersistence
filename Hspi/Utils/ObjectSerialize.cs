using NullGuard;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Hspi.Utils
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal static class ObjectSerialize
    {
        public static string SerializeToString(object obj)
        {
            return Convert.ToBase64String(SerializeToBytes(obj), Base64FormattingOptions.None);
        }

        public static object DeSerializeToObject(string str)
        {
            return DeSerializeFromBytes(Convert.FromBase64String(str));
        }

        public static byte[] SerializeToBytes(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                var binaryFormatter = new BinaryFormatter();

                binaryFormatter.Serialize(memoryStream, obj);

                return memoryStream.ToArray();
            }
        }

        public static object DeSerializeFromBytes([AllowNull]byte[] arrBytes)
        {
            if (arrBytes == null || arrBytes.Length == 0)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                var binaryFormatter = new BinaryFormatter();

                memoryStream.Write(arrBytes, 0, arrBytes.Length);
                memoryStream.Seek(0, SeekOrigin.Begin);

                return binaryFormatter.Deserialize(memoryStream);
            }
        }
    }
}
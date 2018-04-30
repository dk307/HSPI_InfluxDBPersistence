using System;
using System.Runtime.Serialization;

namespace Hspi.Exceptions
{
    [Serializable]
    internal class HspiConnectionException : HspiException
    {
        public HspiConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public HspiConnectionException()
        {
        }

        protected HspiConnectionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
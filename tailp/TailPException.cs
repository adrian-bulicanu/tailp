using System;
using System.Runtime.Serialization;

namespace TailP
{
    [Serializable]
    public class TailPException : Exception
    {
        public TailPException()
        {
        }

        public TailPException(string message) : base(message)
        {
        }

        public TailPException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TailPException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

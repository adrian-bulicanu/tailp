using System;
using System.Runtime.Serialization;

namespace TailP
{
    [Serializable]
    public class TailPExceptionArchive : TailPException
    {
        public TailPExceptionArchive()
        {
        }

        public TailPExceptionArchive(string message) : base(message)
        {
        }

        public TailPExceptionArchive(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TailPExceptionArchive(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

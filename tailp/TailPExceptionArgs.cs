using System;
using System.Runtime.Serialization;

namespace TailP
{
    [Serializable]
    class TailPExceptionArgs : TailPException
    {
        public TailPExceptionArgs()
        {
        }

        public TailPExceptionArgs(string message) : base(message)
        {
        }

        public TailPExceptionArgs(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TailPExceptionArgs(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

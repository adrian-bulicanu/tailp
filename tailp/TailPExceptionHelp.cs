using System;
using System.Runtime.Serialization;

namespace TailP
{
    [Serializable]
    class TailPExceptionHelp : TailPException
    {
        public TailPExceptionHelp()
        {
        }

        public TailPExceptionHelp(string message) : base(message)
        {
        }

        public TailPExceptionHelp(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TailPExceptionHelp(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

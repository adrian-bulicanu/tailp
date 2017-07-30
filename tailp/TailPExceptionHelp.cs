// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
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

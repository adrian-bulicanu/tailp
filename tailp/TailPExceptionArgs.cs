// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
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

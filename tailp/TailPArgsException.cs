// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Runtime.Serialization;

namespace TailP
{
    [Serializable]
    internal class TailPArgsException : TailPException
    {
        public TailPArgsException()
        {
        }

        public TailPArgsException(string message) : base(message)
        {
        }

        public TailPArgsException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TailPArgsException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
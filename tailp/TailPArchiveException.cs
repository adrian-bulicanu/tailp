// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Runtime.Serialization;

namespace TailP
{
    [Serializable]
    public class TailPArchiveException : TailPException
    {
        public TailPArchiveException()
        {
        }

        public TailPArchiveException(string message) : base(message)
        {
        }

        public TailPArchiveException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TailPArchiveException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
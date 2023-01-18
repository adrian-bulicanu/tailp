// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
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

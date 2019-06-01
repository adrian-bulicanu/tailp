// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Runtime.Serialization;

namespace TailP
{
    [Serializable]
    internal class TailPHelpException : TailPException
    {
        public TailPHelpException()
        {
        }

        public TailPHelpException(string message) : base(message)
        {
        }

        public TailPHelpException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TailPHelpException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
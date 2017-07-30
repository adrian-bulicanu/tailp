// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
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

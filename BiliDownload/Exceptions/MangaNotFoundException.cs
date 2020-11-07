﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliDownload.Exceptions
{

    [Serializable]
    public class MangaNotFoundException : Exception
    {
        public MangaNotFoundException() { }
        public MangaNotFoundException(string message) : base(message) { }
        public MangaNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected MangaNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

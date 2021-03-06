﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Toy
{
    class FailToDownloadException : System.Exception
    {
        public FailToDownloadException(string what) : base(what) { }
    }
    class BlockNotPresentException : System.Exception
    {
        public BlockNotPresentException(string what) : base(what) { }
    }
    class HasBeenDownloadException : System.Exception
    {
        public HasBeenDownloadException(string what) : base(what) { }
    }
    class FailToConnectException : System.Exception
    {
        public FailToConnectException(string what) : base(what) { }
    }
}

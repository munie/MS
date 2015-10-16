using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Mnn.MnnSock.Deprecated
{
    /// <summary>
    /// EventArgs for listener events like "listenerStarted"
    /// </summary>
    public class ListenEventArgs : EventArgs
    {
        public ListenEventArgs(EndPoint ep)
        {
            ListenEP = (IPEndPoint)ep;
        }
        public ListenEventArgs(IPEndPoint ep)
        {
            ListenEP = ep;
        }

        public IPEndPoint ListenEP { get; set; }
    }
}

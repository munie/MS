using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace MnnSocket
{
    /// <summary>
    /// EventArgs for listener events like "listenerStarted"
    /// </summary>
    public class ListenerEventArgs : EventArgs
    {
        public ListenerEventArgs(EndPoint ep)
        {
            ListenEP = (IPEndPoint)ep;
        }
        public ListenerEventArgs(IPEndPoint ep)
        {
            ListenEP = ep;
        }

        public IPEndPoint ListenEP { get; set; }
    }
}

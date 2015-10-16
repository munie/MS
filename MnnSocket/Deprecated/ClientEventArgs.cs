using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Mnn.MnnSock.Deprecated
{
    /// <summary>
    /// EventArgs for client events like "clientConnect"
    /// </summary>
    public class ClientEventArgs : EventArgs
    {
        public ClientEventArgs(EndPoint lep, EndPoint rep, byte[] msg)
        {
            LocalEP = (IPEndPoint)lep;
            RemoteEP = (IPEndPoint)rep;
            Data = msg;
        }
        public ClientEventArgs(IPEndPoint lep, IPEndPoint rep, byte[] msg)
        {
            LocalEP = lep;
            RemoteEP = rep;
            Data = msg;
        }

        public IPEndPoint LocalEP { get; set; }
        public IPEndPoint RemoteEP { get; set; }
        public byte[] Data { get; set; }
    }
}

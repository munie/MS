using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace MnnSocket
{
    /// <summary>
    /// EventArgs for client events like "clientConnect"
    /// </summary>
    public class ClientEventArgs : EventArgs
    {
        public ClientEventArgs(EndPoint lep, EndPoint rep, string msg)
        {
            LocalEP = (IPEndPoint)lep;
            RemoteEP = (IPEndPoint)rep;
            Data = msg;
        }
        public ClientEventArgs(IPEndPoint lep, IPEndPoint rep, string msg)
        {
            LocalEP = lep;
            RemoteEP = rep;
            Data = msg;
        }

        public IPEndPoint LocalEP { get; set; }
        public IPEndPoint RemoteEP { get; set; }
        public string Data { get; set; }
    }
}

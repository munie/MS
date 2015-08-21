using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;

namespace Mnn.MnnSocket
{
    public class AsyncSocketListenManager
    {
        public List<AsyncSocketListenItem> Items = new List<AsyncSocketListenItem>();


        /// <summary>
        /// Send data to specified EP
        /// </summary>
        /// <param name="ep"></param>
        /// <param name="data"></param>
        public void Send(IPEndPoint ep, byte[] data)
        {
            var subset = from s in Items where s.ContainClient(ep) == true select s;

            if (subset.Count() != 0)
                subset.First().Send(ep, data);
        }

        /// <summary>
        /// Close specified socket of Client
        /// </summary>
        /// <param name="remoteEP"></param>
        public void CloseClient(IPEndPoint ep)
        {
            var subset = from s in Items where s.ContainClient(ep) == true select s;

            if (subset.Count() != 0)
                subset.First().CloseClient(ep);
        }
    }
}

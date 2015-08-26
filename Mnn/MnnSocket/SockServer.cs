using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Mnn.MnnSocket
{
    public abstract class SockServer
    {
        // Events of listener and client
        public abstract event EventHandler<ListenEventArgs> ListenStarted;
        public abstract event EventHandler<ListenEventArgs> ListenStopped;
        public abstract event EventHandler<ClientEventArgs> ClientReadMsg;
        public abstract event EventHandler<ClientEventArgs> ClientSendMsg;

        // Methods ============================================================================

        /// <summary>
        /// Start TcpServer
        /// </summary>
        /// <param name="ep"></param>
        public abstract void Start(IPEndPoint ep);

        /// <summary>
        /// Stop TcpServer
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Send data to specified client
        /// </summary>
        /// <param name="ep"></param>
        /// <param name="data"></param>
        public abstract void Send(IPEndPoint ep, byte[] data);

    }
}

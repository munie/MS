using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace Mnn.MnnSocket
{
    public class UdpServer : SockServer
    {
        private Socket server;
        private byte[] readbuffer = new byte[8192];
        private bool isExitThread = false;

        // Events of listener and client
        public override event EventHandler<ListenEventArgs> ListenStarted;
        public override event EventHandler<ListenEventArgs> ListenStopped;
        public override event EventHandler<ClientEventArgs> ClientReadMsg;
        public override event EventHandler<ClientEventArgs> ClientSendMsg;

        // Methods ============================================================================

        public override void Start(IPEndPoint ep)
        {
            System.Threading.Thread thread = new System.Threading.Thread(() => {
                server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                server.Bind(ep);

                /// ** Report ListenerStarted event
                if (ListenStarted != null)
                    ListenStarted(this, new ListenEventArgs(ep));

                EndPoint epClient = new IPEndPoint(IPAddress.Any, 0);
                isExitThread = false;
                while (true) {
                    if (server.Poll(500000, SelectMode.SelectRead)) {
                        int bytesRead = server.ReceiveFrom(readbuffer, readbuffer.Length, SocketFlags.None, ref epClient);

                        /// ** Report ClientReadMsg event
                        if (ClientReadMsg != null)
                            ClientReadMsg(this, new ClientEventArgs(ep, epClient, readbuffer.Take(bytesRead).ToArray()));
                    }
                    else if (isExitThread == true) {
                        isExitThread = false;
                        break;
                    }
                }

                server.Close();

                /// ** Report ListenerStopped event
                if (ListenStopped != null)
                    ListenStopped(this, new ListenEventArgs(ep));
            });

            thread.IsBackground = true;
            thread.Start();
        }

        public override void Stop()
        {
            isExitThread = true;
        }

        public override void Send(IPEndPoint ep, byte[] data)
        {
            server.SendTo(data, ep);

            /// ** Report ClientSendMsg event
            if (ClientSendMsg != null)
                ClientSendMsg(this, new ClientEventArgs(server.LocalEndPoint, ep, data));
        }
    }
}

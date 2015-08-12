using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Collections;
using Mnn.MnnUtil;

namespace Mnn.MnnSocket
{
    public class TcpServer
    {
        // listen socket
        private Socket server;
        // listen & accept sockets
        private ArrayList socketList = new ArrayList();
        // buffer for reading
        private byte[] readbuffer = new byte[8192];

        // Events of listener and client
        public event EventHandler<ListenerEventArgs> ListenerStarted;
        public event EventHandler<ListenerEventArgs> ListenerStopped;
        public event EventHandler<ClientEventArgs> ClientConnect;
        public event EventHandler<ClientEventArgs> ClientDisconn;
        public event EventHandler<ClientEventArgs> ClientReadMsg;
        public event EventHandler<ClientEventArgs> ClientSendMsg;

        /// <summary>
        /// Start TcpServer
        /// </summary>
        /// <param name="ep"></param>
        public void Start(IPEndPoint ep)
        {
            Thread thread = new Thread(() =>
            {
                // Verify IPEndPoints
                IPEndPoint[] globalEPs = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                foreach (IPEndPoint globalEP in globalEPs) {
                    if (ep.Equals(globalEP))
                        throw new ApplicationException(ep.ToString() + " is in listening.");
                }

                // Initialize the listenEP field of ListenerState
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server.Bind(ep);
                server.Listen(100);
                socketList.Add(server);

                /// ** Report ListenerStarted event
                if (ListenerStarted != null)
                    ListenerStarted(this, new ListenerEventArgs(ep));

                while (true) {
                    ArrayList list = (ArrayList)socketList.Clone();
                    if (list.Count == 0)
                        break;

                    Socket.Select(list, null, null, -1);
                    HandleSelect(list);
                }

                /// ** Report ListenerStopped event
                if (ListenerStopped != null)
                    ListenerStopped(this, new ListenerEventArgs(ep));
            });

            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// The complex logic for handling select
        /// </summary>
        /// <param name="list"></param>
        private void HandleSelect(ArrayList list)
        {
            lock (socketList) {
                foreach (Socket item in list) {
                    // Server: accept clients
                    if (item == server) {
                        try {
                            Socket client = item.Accept();
                            socketList.Add(client);

                            /// ** Report ClientConnect event
                            if (ClientConnect != null)
                                ClientConnect(this, new ClientEventArgs(client.LocalEndPoint, client.RemoteEndPoint, null));

                        }
                        catch (Exception) {
                            socketList.Remove(item);
                            item.Dispose();
                        }
                        continue;
                    }

                    // Clients: read bytes from clients 
                    int bytesRead = 0;
                    try {
                        bytesRead = item.Receive(readbuffer, 0, readbuffer.Length, 0);
                    }
                    catch (Exception) { }

                    if (bytesRead == 0) {
                        socketList.Remove(item);

                        /// ** Report ClientConnect event
                        if (ClientDisconn != null)
                            ClientDisconn(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint, null));

                    }
                    else {
                        /// ** Report ClientReadMsg event
                        if (ClientReadMsg != null)
                            ClientReadMsg(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint,
                                                            UTF8Encoding.Default.GetString(readbuffer, 0, bytesRead)));
                    }
                }
            }
        }

        /// <summary>
        /// Stop TcpServer
        /// </summary>
        public void Stop()
        {
            // Close server
            server.Close(1);

            // Close clients, there're no server in socketList now
            CloseClient();
        }

        /// <summary>
        /// Send data to all clients
        /// </summary>
        /// <param name="data"></param>
        public void Send(string data)
        {
            lock (socketList) {
                foreach (Socket item in socketList) {
                    if (item == server)
                        continue;

                    item.Send(UTF8Encoding.Default.GetBytes(data), 0, UTF8Encoding.Default.GetBytes(data).Length, 0);

                    /// ** Report ClientSendMsg event
                    if (ClientSendMsg != null)
                        ClientSendMsg(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint, data));
                }
            }
        }

        /// <summary>
        /// Send data to specified client
        /// </summary>
        /// <param name="ep"></param>
        /// <param name="data"></param>
        public void Send(IPEndPoint ep, string data)
        {
            lock (socketList) {
                foreach (Socket item in socketList) {
                    if (item.RemoteEndPoint.Equals(ep)) {
                        item.Send(UTF8Encoding.Default.GetBytes(data), 0, UTF8Encoding.Default.GetBytes(data).Length, 0);

                        /// ** Report ClientSendMsg event
                        if (ClientSendMsg != null)
                            ClientSendMsg(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint, data));

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Close all socket of clients
        /// </summary>
        public void CloseClient()
        {
            lock (socketList) {
                // Stop all client socket
                foreach (Socket item in socketList) {
                    if (item == server)
                        continue;

                    try {
                        item.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception) { }
                }
            }
        }

        /// <summary>
        /// Close specified socket of client
        /// </summary>
        /// <param name="remoteEP"></param>
        public void CloseClient(IPEndPoint ep)
        {
            lock (socketList) {
                // Find target from clientState
                foreach (Socket item in socketList) {
                    // Close this state & The ReadCallback will remove its ClientState
                    if (item.RemoteEndPoint.Equals(ep)) {
                        item.Shutdown(SocketShutdown.Both);
                        break;
                    }
                }
            }
        }


    }
}

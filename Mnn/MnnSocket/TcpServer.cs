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
using System.Runtime.InteropServices;

namespace Mnn.MnnSocket
{
    public class TcpServer
    {
        // listen socket
        private Socket server;
        // client sockets
        //private ArrayList socketList = new ArrayList();
        private List<Socket> socketTable = new List<Socket>();
        // removing client sockets
        private List<Socket> socketRemoving = new List<Socket>();
        // socket locker for socketTable & socketRemoving
        private string socketLocker = "Socket Locker"; 
        // buffer for reading
        private byte[] readbuffer = new byte[8192];

        // Events of listener and client
        public event EventHandler<ListenerEventArgs> ListenerStarted;
        public event EventHandler<ListenerEventArgs> ListenerStopped;
        public event EventHandler<ClientEventArgs> ClientConnect;
        public event EventHandler<ClientEventArgs> ClientDisconn;
        public event EventHandler<ClientEventArgs> ClientReadMsg;
        public event EventHandler<ClientEventArgs> ClientSendMsg;

        private byte[] KeepAliveTime
        {
            get
            {
                uint dummy = 0;
                byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
                BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
                BitConverter.GetBytes((uint)5000).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
                BitConverter.GetBytes((uint)5000).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);
                return inOptionValues;
            }
        }

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
                socketTable.Add(server);

                /// ** Report ListenerStarted event
                if (ListenerStarted != null)
                    ListenerStarted(this, new ListenerEventArgs(ep));

                while (true) {
                    lock (socketLocker) {
                        if (socketRemoving.Count != 0) {
                            foreach (var item in socketRemoving) {
                                if (socketTable.Contains(item)) {
                                    socketTable.Remove(item);

                                    if (item == server) {
                                        /// ** Report ListenerStopped event
                                        if (ListenerStopped != null)
                                            ListenerStopped(this, new ListenerEventArgs(item.LocalEndPoint));
                                        item.Close();
                                    }
                                    else {
                                        /// ** Report ClientConnect event
                                        if (ClientDisconn != null)
                                            ClientDisconn(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint, null));
                                        item.Close();
                                    }
                                }
                            }
                            socketRemoving.Clear();
                        }
                    }

                    if (socketTable.Count == 0)
                        break;

                    // 仅涉及 socketTable 的读取，在本线程内无需加锁
                    ArrayList list = new ArrayList(socketTable);

                    Socket.Select(list, null, null, 500);
                    HandleSelect(list);
                }
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
            lock (socketLocker) {
                foreach (Socket item in list) {
                    // Server: accept clients
                    if (item == server) {
                        try {
                            Socket client = item.Accept();
                            socketTable.Add(client);
                            client.IOControl(IOControlCode.KeepAliveValues, KeepAliveTime, null);

                            /// ** Report ClientConnect event
                            if (ClientConnect != null)
                                ClientConnect(this, new ClientEventArgs(client.LocalEndPoint, client.RemoteEndPoint, null));

                        }
                        catch (Exception) { }
                        continue;
                    }

                    // Clients: read bytes from clients 
                    int bytesRead = 0;
                    try {
                        bytesRead = item.Receive(readbuffer, 0, readbuffer.Length, 0);
                    }
                    catch (Exception) { }

                    if (bytesRead == 0) {
                        socketRemoving.Add(item);
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
            // Close all
            lock (socketLocker) {
                socketRemoving.AddRange(socketTable);
            }
        }

        /// <summary>
        /// Send data to all clients
        /// </summary>
        /// <param name="data"></param>
        public void Send(string data)
        {
            lock (socketLocker) {
                foreach (Socket item in socketTable) {
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
            lock (socketLocker) {
                foreach (Socket item in socketTable) {
                    if (item == server)
                        continue;

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
            lock (socketLocker) {
                foreach (var item in socketTable) {
                    if (item != server)
                        socketRemoving.Add(item);
                }
            }
        }

        /// <summary>
        /// Close specified socket of client
        /// </summary>
        /// <param name="remoteEP"></param>
        public void CloseClient(IPEndPoint ep)
        {
            lock (socketLocker) {
                foreach (var item in socketTable) {
                    try {
                        if (item == server)
                            continue;
                        if (item.RemoteEndPoint.Equals(ep)) {
                            socketRemoving.Add(item);
                            break;
                        }
                    }
                    catch (Exception) { }
                }
            }
        }


    }
}

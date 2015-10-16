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

namespace Mnn.MnnSock.Deprecated
{
    public class TcpServer : SockServer
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

        // Events of listener and client
        public override event EventHandler<ListenEventArgs> ListenStarted;
        public override event EventHandler<ListenEventArgs> ListenStopped;
        public override event EventHandler<ClientEventArgs> ClientReadMsg;
        public override event EventHandler<ClientEventArgs> ClientSendMsg;
        public event EventHandler<ClientEventArgs> ClientConnect;
        public event EventHandler<ClientEventArgs> ClientDisconn;

        // Methods ============================================================================

        /// <summary>
        /// Start TcpServer
        /// </summary>
        /// <param name="ep"></param>
        public override void Start(IPEndPoint ep)
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

            Thread thread = new Thread(() =>
            {
                /// ** Report ListenerStarted event
                if (ListenStarted != null)
                    ListenStarted(this, new ListenEventArgs(ep));

                while (true) {
                    lock (socketLocker) {
                        if (socketRemoving.Count != 0) {
                            foreach (var item in socketRemoving) {
                                if (socketTable.Contains(item)) {
                                    socketTable.Remove(item);

                                    if (item == server) {
                                        /// ** Report ListenerStopped event
                                        if (ListenStopped != null)
                                            ListenStopped(this, new ListenEventArgs(item.LocalEndPoint));
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

                    // 只有主线程可以写 socketTable， 所以主线程的 socketTable 读取，在主线程内无需加锁
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
                    if (item == server && !socketRemoving.Contains(server)) {
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
                            ClientReadMsg(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint, readbuffer.Take(bytesRead).ToArray()));
                    }
                }
            }
        }

        /// <summary>
        /// Stop TcpServer
        /// </summary>
        public override void Stop()
        {
            // Close all
            lock (socketLocker) {
                socketRemoving.AddRange(socketTable);
            }
        }

        /// <summary>
        /// Send data to specified client
        /// </summary>
        /// <param name="ep"></param>
        /// <param name="data"></param>
        public override void Send(IPEndPoint ep, byte[] data)
        {
            lock (socketLocker) {
                foreach (Socket item in socketTable) {
                    if (item == server)
                        continue;

                    if (item.RemoteEndPoint.Equals(ep)) {
                        item.Send(data, 0, data.Length, 0);

                        /// ** Report ClientSendMsg event
                        if (ClientSendMsg != null)
                            ClientSendMsg(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint, data));

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Send data to all clients
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
        {
            lock (socketLocker) {
                foreach (Socket item in socketTable) {
                    if (item == server)
                        continue;

                    item.Send(data, 0, data.Length, 0);

                    /// ** Report ClientSendMsg event
                    if (ClientSendMsg != null)
                        ClientSendMsg(this, new ClientEventArgs(item.LocalEndPoint, item.RemoteEndPoint, data));

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

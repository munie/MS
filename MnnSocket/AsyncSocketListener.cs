using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;

namespace MnnSocket
{
    public partial class AsyncSocketListener
    {
        /// <summary>
        /// Definition for pursuing listener's state
        /// </summary>
        class ListenerState
        {
            // Listen IPEndPoint
            public IPEndPoint listenEP = null;
            // Listen Socket
            public Socket listenSocket = null;
            // Run the listen function
            public Thread Thread = null;
        }

        /// <summary>
        /// Definition for recording connected client's state
        /// </summary>
        class ClientState
        {
            // Listen Socket's IPEndPoint
            public IPEndPoint localEP = null;
            // Accept result's IPEndPoint
            public IPEndPoint remoteEP = null;
            // Client Socket.
            public Socket workSocket = null;
            // Size of receive buffer.
            public const int BufferSize = 2048;
            // Receive buffer.
            public byte[] buffer = new byte[BufferSize];
            // Received data string.
            public StringBuilder sb = new StringBuilder();
        }
    }
    
    /// <summary>
    /// A asynchronous socket listener, which can listen at multiple IPEndPoint.
    /// </summary>
    public partial class AsyncSocketListener
    {
        // Constructor
        public AsyncSocketListener() { }

        // List of ListenerState
        private List<ListenerState> listenerState = new List<ListenerState>();
        // List of ClientState
        private List<ClientState> clientState = new List<ClientState>();

        // Events of listener and client
        public event EventHandler<ListenerEventArgs> ListenerStarted;
        public event EventHandler<ListenerEventArgs> ListenerStopped;
        public event EventHandler<ClientEventArgs> ClientConnect;
        public event EventHandler<ClientEventArgs> ClientDisconn;
        public event EventHandler<ClientEventArgs> ClientReadMsg;
        public event EventHandler<ClientEventArgs> ClientSendMsg;

        // Methods ================================================================
        /// <summary>
        /// Start AsyncSocketListener
        /// </summary>
        /// <param name="localEPs"></param>
        /// <exception cref="System.ApplicationException"></exception>
        /// <exception cref="System.Net.Sockets.SocketException"></exception>
        /// <exception cref="System.ObjectDisposedException"></exception>
        public void Start(List<IPEndPoint> startEPs)
        {
            // If eps in localEPs are unique to each other
            for (int i = 0; i < startEPs.Count - 1; i++) {
                for (int j = i + 1; j < startEPs.Count; j++) {
                    if (startEPs[i].Equals(startEPs[j]))
                        throw new ApplicationException("The eps in startEPs are not unique to each other.");
                }
            }

            // Verify IPEndPoints
            IPEndPoint[] globalEPs = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (IPEndPoint startEP in startEPs) {
                foreach (IPEndPoint globalEP in globalEPs) {
                    if (startEP.Equals(globalEP))
                        throw new ApplicationException(startEP.ToString() + " is in listening.");
                }
            }

            lock (listenerState) {
                // Instantiate ListenerState & Start listening
                foreach (IPEndPoint ep in startEPs) {
                    ListenerState lstState = new ListenerState();
                    listenerState.Add(lstState);

                    // Initialize the listenEP field of ListenerState
                    lstState.listenEP = ep;

                    // Initialize Socket
                    lstState.listenSocket = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream, ProtocolType.Tcp);
                    lstState.listenSocket.Bind(ep);
                    lstState.listenSocket.Listen(100);

                    // Create a thread for running listenSocket
                    lstState.Thread = new Thread(new ParameterizedThreadStart(ListeningThread));
                    lstState.Thread.IsBackground = true;
                    lstState.Thread.Start(lstState);
                }
            }
        }

        /// <summary>
        /// Stop all AsyncSocketListener
        /// </summary>
        public void Stop()
        {
            lock (listenerState) {
                foreach (ListenerState lstState in listenerState) {
                    // Stop listener & The ListeningThread will stop by its exception automaticlly
                    lstState.listenSocket.Dispose();

                    // Ensure to abort the ListeningThread
                    lstState.Thread.Abort();
                    //lstState.Thread.Join();   //死锁
                    lstState.Thread = null;
                }
            }
        }

        /// <summary>
        /// Stop specified AsyncSocketListener
        /// </summary>
        /// <param name="localEPs"></param>
        /// <exception cref="System.ApplicationException"></exception>
        public void Stop(List<IPEndPoint> stopEPs)
        {
            // If eps in localEPs are unique to each other
            for (int i = 0; i < stopEPs.Count - 1; i++) {
                for (int j = i + 1; j < stopEPs.Count; j++) {
                    if (stopEPs[i].Equals(stopEPs[j]))
                        throw new ApplicationException("The eps in stopEPs are not unique to each other.");
                }
            }

            // Verify IPEndPoints
            IPEndPoint[] globalEPs = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (IPEndPoint stopEP in stopEPs) {
                var subset = from ep in globalEPs where ep.Equals(stopEP) select ep;
                if (subset.Count() == 0)
                    throw new ApplicationException("Specified stopEP is not at listening.");
            }

            lock (listenerState) {
                // Stop listener and its thread & The thread will remove its ListenerState
                foreach (IPEndPoint ep in stopEPs) {
                    foreach (ListenerState lstState in listenerState) {
                        if (ep.Equals(lstState.listenSocket.LocalEndPoint)) {
                            // Stop listener & The ListeningThread will stop by its exception automaticlly
                            lstState.listenSocket.Dispose();

                            // Ensure to abort the ListeningThread
                            lstState.Thread.Abort();
                            //lstState.Thread.Join();   //死锁
                            Thread.Sleep(200);          //不死锁，却影响响应时间
                            lstState.Thread = null;

                            break;
                        }
                    }
                }
            }
        }

        private void ListeningThread(object args)
        {
            ListenerState lstState = args as ListenerState;

            try {
                /// ** Report ListenerStarted event
                if (ListenerStarted != null)
                    ListenerStarted(this, new ListenerEventArgs(lstState.listenEP));

                while (true) {
                    // Start an asynchronous socket to listen for connections.
                    // Get the socket that handles the client request.
                    Socket handler = lstState.listenSocket.Accept();

                    // Create the state object.
                    ClientState cltState = new ClientState();
                    cltState.localEP = (IPEndPoint)handler.LocalEndPoint;
                    cltState.remoteEP = (IPEndPoint)handler.RemoteEndPoint;
                    cltState.workSocket = handler;

                    /// @@ 第一次收到数据后，才会进入ReadCallback。
                    /// @@ 所以在没有收到数据前，线程被迫中止，将进入ReadCallback，并使EndReceive产生异常
                    /// @@ 由于ReadCallback在出现异常时，自动关闭client Socket连接，所有本线程Accept的连接都将断开并删除
                    /// @@ 暂时想不出解决办法
                    // Start receive message from client
                    handler.BeginReceive(cltState.buffer, 0, ClientState.BufferSize, 0,
                        new AsyncCallback(ReadCallback), cltState);

                    // Add handler to ClientState
                    lock (clientState) {
                        clientState.Add(cltState);
                    }

                    /// ** Report ClientConnect event
                    if (ClientConnect != null)
                        ClientConnect(this, new ClientEventArgs(cltState.localEP, cltState.remoteEP, null));
                }
            }
            catch (Exception ex) {
                lock (listenerState) {
                    // Remove lstState form listenerState
                    if (listenerState.Contains(lstState)) {
                        lstState.listenSocket.Dispose();
                        listenerState.Remove(lstState);
                    }
                }

                /// ** Report ListenerStopped event
                if (ListenerStopped != null)
                    ListenerStopped(this, new ListenerEventArgs(lstState.listenEP));

                Console.WriteLine(ex.ToString());
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            ClientState cltState = (ClientState)ar.AsyncState;

            try {
                // Read data from the client socket. 
                int bytesRead = cltState.workSocket.EndReceive(ar);

                if (bytesRead > 0) {
                    // There  might be more data, so store the data received so far.
                    cltState.sb.Append(UTF8Encoding.Default.GetString(
                        cltState.buffer, 0, bytesRead));

                    /// ** Report ClientReadMsg event
                    if (ClientReadMsg != null)
                        ClientReadMsg(this, new ClientEventArgs(cltState.localEP, cltState.remoteEP, cltState.sb.ToString()));

                    // Then clear the StringBuilder
                    cltState.sb.Clear();

                    // Restart receive message from client
                    cltState.workSocket.BeginReceive(cltState.buffer, 0, ClientState.BufferSize, 0,
                        new AsyncCallback(ReadCallback), cltState);
                }
                else {
                    // Just for closing the client socket, so the ErrorCode is Shutdown
                    throw new SocketException((int)SocketError.Shutdown);
                }
            }
            //catch (SocketException ex) {
            //}
            catch (Exception ex) {
                // Close socket of client & remove it form clientState
                lock (clientState) {
                    if (clientState.Contains(cltState)) {
                            //cltState.workSocket.Shutdown(SocketShutdown.Both);    // 已经调用Dispose()将引起内存访问错误
                            cltState.workSocket.Dispose();
                            clientState.Remove(cltState);
                    }
                }

                /// ** Report ClientDisconn event
                if (ClientDisconn != null)
                    ClientDisconn(this, new ClientEventArgs(cltState.localEP, cltState.remoteEP, null));

                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Send data to specified EP
        /// </summary>
        /// <param name="ep"></param>
        /// <param name="data"></param>
        public void Send(IPEndPoint ep, string data)
        {
            lock (clientState) {
                foreach (ClientState cltState in clientState) {
                    if (cltState.workSocket.RemoteEndPoint.Equals(ep)) {
                        cltState.workSocket.BeginSend(UTF8Encoding.Default.GetBytes(data), 0,
                           UTF8Encoding.Default.GetBytes(data).Length, 0,
                           new AsyncCallback(SendCallback), cltState);

                    /// ** Report ClientSendMsg event
                    if (ClientSendMsg != null)
                        ClientSendMsg(this, new ClientEventArgs(cltState.localEP, cltState.remoteEP, data));
                    }
                }
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try {
                // Retrieve the state object and the handler socket
                // from the asynchronous state object.
                ClientState cltState = (ClientState)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = cltState.workSocket.EndSend(ar);
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Close all socket of Client
        /// </summary>
        public void CloseClient()
        {
            lock (clientState) {
                // Stop all client socket
                foreach (ClientState cltState in clientState) {
                    //cltState.workSocket.Shutdown(SocketShutdown.Both);
                    cltState.workSocket.Dispose();
                }
            }
        }

        /// <summary>
        /// Close specified socket of Client
        /// </summary>
        /// <param name="remoteEP"></param>
        public void CloseClient(IPEndPoint ep)
        {
            lock (clientState) {
                // Find target from clientState
                foreach (ClientState cltState in clientState) {
                    // Close this state & The ReadCallback will remove its ClientState
                    if (cltState.remoteEP.Equals(ep)) {
                        //cltState.workSocket.Shutdown(SocketShutdown.Both);
                        cltState.workSocket.Dispose();

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Close all client socket accepted by specified localEP
        /// </summary>
        /// <param name="localEP"></param>
        public void CloseClientByListener(IPEndPoint listenerEP)
        {
            lock (clientState) {
                // Find target from clientState
                foreach (ClientState cltState in clientState) {
                    // Close this state & The ReadCallback will remove its ClientState
                    if (cltState.localEP.Equals(listenerEP)) {
                        //cltState.workSocket.Shutdown(SocketShutdown.Both);
                        cltState.workSocket.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Get Status of listeners and clients
        /// </summary>
        public void GetStatus()
        {
        }

        /// <summary>
        /// Get Socket specified EP
        /// </summary>
        /// <param name="ep"></param>
        /// <returns></returns>
        public Socket GetSocket(IPEndPoint ep)
        {
            lock (clientState) {
                foreach (ClientState cltState in clientState) {
                    if (cltState.workSocket.RemoteEndPoint.Equals(ep)) {
                        return cltState.workSocket;
                    }
                }

                return null;
            }
        }
    }
}

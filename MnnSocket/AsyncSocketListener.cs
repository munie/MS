using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MnnSocket
{
    // Definiton of Delegate for listener and client
    public delegate void ListenerStartedEventHandler(object sender, ListenerEventArgs e);
    public delegate void ListenerStoppedEventHandler(object sender, ListenerEventArgs e);
    public delegate void ClientConnectEventHandler(object sender, ClientEventArgs e);
    public delegate void ClientDisconnEventHandler(object sender, ClientEventArgs e);
    public delegate void ClientMessageEventHandler(object sender, ClientEventArgs e);

    // EventArgs for listener and client Event
    public class ListenerEventArgs : EventArgs
    {
        public ListenerEventArgs(EndPoint ep)
        {
            listenEP = (IPEndPoint)ep;
        }
        public ListenerEventArgs(IPEndPoint ep)
        {
            listenEP = ep;
        }

        public IPEndPoint listenEP { get; set; }
    }
    public class ClientEventArgs : EventArgs
    {
        public ClientEventArgs(EndPoint ep, string msg)
        {
            clientEP = (IPEndPoint)ep;
            data = msg;
        }
        public ClientEventArgs(IPEndPoint ep, string msg)
        {
            clientEP = ep;
            data = msg;
        }

        public IPEndPoint clientEP { get; set; }
        public string data { get; set; }
    }

    /// <summary>
    /// Definition for pursuing listener's state
    /// </summary>
    class ListenerState
    {
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
        // Client  socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 2048;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }
    
    /// <summary>
    /// A asynchronous socket listener, which can listen at multiple IPEndPoint.
    /// </summary>
    public class AsyncSocketListener
    {
        // Constructor
        public AsyncSocketListener()
        {
        }

        // List of ListenerState
        private List<ListenerState> listenerState = new List<ListenerState>();
        // List of ClientState
        private List<ClientState> clientState = new List<ClientState>();

        // Message receive event
        public event ListenerStartedEventHandler listenerStarted;
        public event ListenerStoppedEventHandler listenerStopped;
        public event ClientConnectEventHandler clientConnect;
        public event ClientDisconnEventHandler clientDisconn;
        public event ClientMessageEventHandler clientMessage;

        // Methods ================================================================
        /// <summary>
        /// Start AsyncSocketListener
        /// </summary>
        /// <param name="localEPs"></param>
        /// <exception cref="System.ApplicationException"></exception>
        /// <exception cref="System.Net.Sockets.SocketException"></exception>
        /// <exception cref="System.ObjectDisposedException"></exception>
        public void Start(List<IPEndPoint> localEPs)
        {
            // If eps in localEPs are unique to each other
            for (int i = 0; i < localEPs.Count - 1; i++) {
                for (int j = i + 1; j < localEPs.Count; j++) {
                    if (localEPs[i].Equals(localEPs[j]))
                        throw new ApplicationException("The eps in localEPs are not unique to each other.");
                }
            }

            lock (listenerState) {
                // Verify IPEndPoints
                foreach (ListenerState lstState in listenerState) {
                    if (localEPs.Contains(lstState.listenSocket.LocalEndPoint) == true)
                        //ports.Remove(state.port);
                        throw new ApplicationException("Another AsyncSocketListener is listening at "
                            + lstState.listenSocket.LocalEndPoint.ToString() + ".");
                }

                // Instantiate ListenerState & Start listening
                foreach (IPEndPoint ep in localEPs) {
                    ListenerState lstState = new ListenerState();
                    listenerState.Add(lstState);

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
            lock (this) {
                foreach (ListenerState lstState in listenerState) {
                    // Stop listener & The ListeningThread will stop by its exception automaticlly
                    lstState.listenSocket.Dispose();

                    // Ensure to abort the ListeningThread
                    lstState.Thread.Abort();
                    lstState.Thread.Join();
                    lstState.Thread = null;
                }
                listenerState.Clear();

                // Stop all client socket & Clear the clientState
                foreach (ClientState cltState in clientState) {
                    cltState.workSocket.Shutdown(SocketShutdown.Both);
                    cltState.workSocket.Dispose();
                }
                clientState.Clear();
            }
        }

        /// <summary>
        /// Stop specified AsyncSocketListener
        /// </summary>
        /// <param name="localEPs"></param>
        /// <exception cref="System.ApplicationException"></exception>
        public void Stop(List<IPEndPoint> localEPs)
        {
            // If eps in localEPs are unique to each other
            for (int i = 0; i < localEPs.Count - 1; i++) {
                for (int j = i + 1; j < localEPs.Count; j++) {
                    if (localEPs[i].Equals(localEPs[j]))
                        throw new ApplicationException("The eps in localEPs are not unique to each other.");
                }
            }

            lock (this) {
                // Verify IPEndPoints
                foreach (IPEndPoint ep in localEPs) {
                    bool argsVerify = false;
                    foreach (ListenerState lstState in listenerState) {
                        if (ep.Equals(lstState.listenSocket.LocalEndPoint)) {
                            argsVerify = true;
                            break;
                        }
                    }
                    if (argsVerify == false)
                        throw new ApplicationException("Specified localEP is not at listening.");
                    argsVerify = false;
                }

                // Stop listener and its thread & Remove it from listenerState
                foreach (IPEndPoint ep in localEPs) {
                    foreach (ListenerState lstState in listenerState) {
                        if (ep.Equals(lstState.listenSocket.LocalEndPoint)) {
                            // Stop listener & The ListeningThread will stop by its exception automaticlly
                            lstState.listenSocket.Dispose();

                            // Ensure to abort the ListeningThread
                            lstState.Thread.Abort();
                            lstState.Thread.Join();
                            lstState.Thread = null;

                            // Remove lstState from listenerState
                            listenerState.Remove(lstState);

                            // Skip for loop, So the forehead "listenerState.Remove" meothod is right.
                            break;
                        }
                    }
                }

                // If listenerState is clear, then Stop all client socket & Clear the clientState
                if (listenerState.Count == 0) {
                    // Stop all client socket & Clear the clientState
                    foreach (ClientState cltState in clientState) {
                        cltState.workSocket.Shutdown(SocketShutdown.Both);
                        cltState.workSocket.Dispose();
                    }
                    clientState.Clear();
                }
            }
        }

        private void ListeningThread(object args)
        {
            ListenerState lstState = args as ListenerState;
            IPEndPoint epBackup = (IPEndPoint)lstState.listenSocket.LocalEndPoint;

            try {
                /// ** Report listenerStarted event
                if (listenerStarted != null)
                    listenerStarted(this, new ListenerEventArgs(epBackup));

                while (true) {
                    // Start an asynchronous socket to listen for connections.
                    // Get the socket that handles the client request.
                    Socket handler = lstState.listenSocket.Accept();

                    // Create the state object.
                    ClientState cltState = new ClientState();
                    cltState.workSocket = handler;

                    // Start receive message from client
                    handler.BeginReceive(cltState.buffer, 0, ClientState.BufferSize, 0,
                        new AsyncCallback(ReadCallback), cltState);

                    // Add handler to ClientState
                    lock (clientState) {
                        clientState.Add(cltState);
                    }

                    /// ** Report clientConnect event
                    if (clientConnect != null)
                        clientConnect(this, new ClientEventArgs(handler.RemoteEndPoint, null));
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

                /// ** Report listenerStopped event
                if (listenerStopped != null)
                    listenerStopped(this, new ListenerEventArgs(epBackup));

                Console.WriteLine(ex.ToString());
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            ClientState cltState = (ClientState)ar.AsyncState;
            IPEndPoint epBackup = (IPEndPoint)cltState.workSocket.RemoteEndPoint;

            try {
                // Read data from the client socket. 
                int bytesRead = cltState.workSocket.EndReceive(ar);

                if (bytesRead > 0) {
                    // There  might be more data, so store the data received so far.
                    cltState.sb.Append(UTF8Encoding.Default.GetString(
                        cltState.buffer, 0, bytesRead));

                    /// ** Report clientMessage event
                    if (clientMessage != null)
                        clientMessage(this, new ClientEventArgs(cltState.workSocket.RemoteEndPoint, cltState.sb.ToString()));

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
            catch (SocketException ex) {
                // Close socket of client & remove it form clientState
                lock (clientState) {
                    if (clientState.Contains(cltState)) {
                        cltState.workSocket.Shutdown(SocketShutdown.Both);
                        cltState.workSocket.Dispose();
                        clientState.Remove(cltState);
                    }
                }

                /// ** Report clientDisconn event
                if (clientDisconn != null)
                    clientDisconn(this, new ClientEventArgs(epBackup, null));

                Console.WriteLine(ex.ToString());
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Send(IPEndPoint ep, string data)
        {
            lock (clientState) {
                foreach (ClientState cltState in clientState) {
                    if (cltState.workSocket.RemoteEndPoint.Equals(ep)) {
                        cltState.workSocket.BeginSend(UTF8Encoding.Default.GetBytes(data), 0,
                           UTF8Encoding.Default.GetBytes(data).Length, 0,
                           new AsyncCallback(SendCallback), cltState.workSocket);
                    }
                }
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        public void CloseClient(IPEndPoint ep)
        {
            lock (clientState) {
                // Find target from clientState
                foreach (ClientState cltState in clientState) {
                    // Remove & Close this state
                    if (cltState.workSocket.RemoteEndPoint.Equals(ep)) {
                        cltState.workSocket.Shutdown(SocketShutdown.Both);
                        cltState.workSocket.Dispose();
                        clientState.Remove(cltState);

                        /// ** Report clientDisconn event
                        if (clientDisconn != null)
                            clientDisconn(this, new ClientEventArgs(ep, null));
                    }
                }
            }
        }

        public void GetStatus()
        {

        }
    }
}

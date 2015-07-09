using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MnnSocket
{
    // Definiton of Delegate and it's EventArgs
    public delegate void ListenerStartedEventHandler(object sender, EventArgs e);
    public delegate void ListenerStoppedEventHandler(object sender, EventArgs e);
    public delegate void ClientConnectEventHandler(object sender, ClientEventArgs e);
    public delegate void ClientDisconnEventHandler(object sender, ClientEventArgs e);
    public delegate void ClientMessageEventHandler(object sender, ClientEventArgs e);
    public class ClientEventArgs : EventArgs
    {
        public ClientEventArgs(EndPoint ep, string message)
        {
            ipEndPoint = (IPEndPoint)ep;
            data = message;
        }

        public ClientEventArgs(IPEndPoint ep, string message)
        {
            ipEndPoint = ep;
            data = message;
        }

        public IPEndPoint ipEndPoint { get; set; }
        public string data { get; set; }
    }

    // Definition for recording connected client's state
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
    
    // Main Definition
    public class AsyncSocketListener
    {
        // Constructor
        public AsyncSocketListener()
        {
        }

        // Asynchrous Scoket Listener & Listener Thread & State
        private Socket listener = null;
        private Thread listenerThread = null;
        private bool listenerThreadState = false;

        // List of ClientState
        private List<ClientState> clientState = new List<ClientState>();

        // Message receive event
        public event ListenerStartedEventHandler listenerStarted;
        public event ListenerStoppedEventHandler listenerStopped;
        public event ClientConnectEventHandler clientConnect;
        public event ClientDisconnEventHandler clientDisconn;
        public event ClientMessageEventHandler clientMessage;

        // methods ================================================================
        public void Start(int port)
        {
            if (listenerThreadState == true)
                return;

            listenerThread = new Thread(new ParameterizedThreadStart(ListeningThread));
            listenerThread.IsBackground = true;
            listenerThread.Start(port);

            listenerThreadState = true;
        }

        public void Stop()
        {
            if (listenerThreadState == false)
                return;

            listenerThread.Abort();
            listener.Dispose();
            foreach (ClientState state in clientState) {
                state.workSocket.Shutdown(SocketShutdown.Both);
                state.workSocket.Dispose();
            }
            clientState.Clear();

            listenerThreadState = false;
        }

        private void ListeningThread(object port)
        {
            try {
                // Establish the local endpoint for the socket.
                // The DNS name of the computer
                // running the listener is "host.contoso.com".
                IPAddress[] ipAddr = Dns.GetHostAddresses(Dns.GetHostName());
                IPEndPoint localEndPoint = new IPEndPoint(ipAddr[0], (int)port);
                foreach (IPAddress ip in ipAddr) {
                    if (ip.AddressFamily.Equals(AddressFamily.InterNetwork)) {
                        localEndPoint.Address = ip;
                        break;
                    }
                }

                // Create a TCP/IP socket.
                listener = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);

                // Bind the socket to the local endpoint and listen for incoming connections.
                listener.Bind(localEndPoint);
                listener.Listen(100);

                /// ** Report listenerStarted event
                if (listenerStarted != null)
                    listenerStarted(this, null);

                while (true) {
                    // Start an asynchronous socket to listen for connections.
                    // Get the socket that handles the client request.
                    Socket handler = listener.Accept();

                    // Create the state object.
                    ClientState state = new ClientState();
                    state.workSocket = handler;

                    // Start receive message from client
                    handler.BeginReceive(state.buffer, 0, ClientState.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);

                    // Add handler to ClientState
                    clientState.Add(state);

                    /// ** Report clientConnect event
                    if (clientConnect != null)
                        clientConnect(this, new ClientEventArgs(handler.RemoteEndPoint, null));
                }
            }
            catch (Exception ex) {
                /// ** Report listenerStopped event
                if (listenerStopped != null)
                    listenerStopped(this, null);

                Console.WriteLine(ex.ToString());
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            ClientState state = (ClientState)ar.AsyncState;
            Socket handler = state.workSocket;

            try {
                // Read data from the client socket. 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0) {
                    // There  might be more data, so store the data received so far.
                    state.sb.Append(UTF8Encoding.Default.GetString(
                        state.buffer, 0, bytesRead));

                    /// ** Report clientMessage event
                    if (clientMessage != null)
                        clientMessage(this, new ClientEventArgs(handler.RemoteEndPoint, state.sb.ToString()));

                    // Then clear the StringBuilder
                    state.sb.Clear();

                    // Restart receive message from client
                    handler.BeginReceive(state.buffer, 0, ClientState.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                }
                else {
                    // Just for closing the client socket, so the ErrorCode is Shutdown
                    throw new SocketException((int)SocketError.Shutdown);
                }
            }
            catch (SocketException ex) {
                // Save the RemoteEndPoint of closing handler
                EndPoint ep = handler.RemoteEndPoint;

                // Close socket of client & remove it form clientState
                handler.Shutdown(SocketShutdown.Both);
                handler.Dispose();
                clientState.Remove(state);

                /// ** Report clientDisconn event
                if (clientDisconn != null)
                    clientDisconn(this, new ClientEventArgs(ep, null));

                Console.WriteLine(ex.ToString());
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Send(EndPoint ep, string data)
        {
            foreach (ClientState state in clientState) {
                if (state.workSocket.RemoteEndPoint.Equals(ep)) {
                    state.workSocket.BeginSend(UTF8Encoding.Default.GetBytes(data), 0,
                       UTF8Encoding.Default.GetBytes(data).Length, 0,
                       new AsyncCallback(SendCallback), state.workSocket);
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

        public void CloseClient(EndPoint ep)
        {
            // Find target from clientState
            foreach (ClientState state in clientState) {
                // Remove & Close this state
                if (state.workSocket.RemoteEndPoint.Equals(ep)) {
                    clientState.Remove(state);
                    state.workSocket.Shutdown(SocketShutdown.Both);
                    state.workSocket.Dispose();

                    /// ** Report clientDisconn event
                    if (clientDisconn != null)
                        clientDisconn(this, new ClientEventArgs(ep, null));
                }
            }
        }

        public void GetStatus()
        {

        }
    }
}

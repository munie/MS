using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace MnnSocket
{
    public class AsyncSocketSender
    {
        public enum AsyncState {
            ConnectSuccess = 0,
            ConnectFail = 1,
            Disconncted = 2,
            SendSuccess = 3,
            SendFail = 4,
            ReadMessage = 5,
        }
        public delegate void MessageReceiverDelegate(string s, AsyncState dealerState);
        public event MessageReceiverDelegate messageReceiver;

        private Socket sender;
        private const int bufferSize = 2048;
        private byte[] sendBuffer = new byte[bufferSize];
        private byte[] readBuffer = new byte[bufferSize];

        public AsyncSocketSender()
        {
        }

        public void Connect(EndPoint endPoint)
        {
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sender.BeginConnect(endPoint, new AsyncCallback(ConnectCallback), sender);
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                //Console.WriteLine("Socket connected to {0}",
                //    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.
                //connectDone.Set();

                // Event the one that needs to be evented.
                if (messageReceiver != null)
                    messageReceiver("", AsyncState.ConnectSuccess);

                // start receive message
                Array.Clear(readBuffer, 0, readBuffer.Length);
                sender.BeginReceive(readBuffer, 0, readBuffer.Length, 0,
                    new AsyncCallback(ReceiveCallback), sender);
            }
            catch (Exception ex) {
                sender.Close();
                sender = null;
                if (messageReceiver != null)
                    messageReceiver("", AsyncState.ConnectFail);
                Console.WriteLine(ex.ToString());
            }
        }

        public void DisConn()
        {
            sender.Shutdown(SocketShutdown.Both);
            sender.Close();
            sender = null;

            // Event the one that needs to be evented.
            if (messageReceiver != null)
                messageReceiver("", AsyncState.Disconncted);
        }

        public void Send(string s)
        {
            Array.Clear(sendBuffer, 0, sendBuffer.Length);
            sendBuffer = UTF8Encoding.Default.GetBytes(s);

            sender.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, new AsyncCallback(SendCallback), sender);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);

                if (messageReceiver != null) {
                    if (bytesSent == sendBuffer.Length)
                        messageReceiver("", AsyncState.SendSuccess);
                    else
                        messageReceiver("", AsyncState.SendFail);
                }

            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try {
                Socket client = (Socket)ar.AsyncState;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead == 0) {
                    DisConn();
                    return;
                }

                string str = UTF8Encoding.Default.GetString(readBuffer, 0, readBuffer.Length).Replace("\0", "");

                if (messageReceiver != null) {
                    messageReceiver(str, AsyncState.ReadMessage);
                }

                // restart the receiver
                Array.Clear(readBuffer, 0, readBuffer.Length);
                sender.BeginReceive(readBuffer, 0, readBuffer.Length, 0,
                    new AsyncCallback(ReceiveCallback), sender);
            }
            catch (SocketException ex) {
                DisConn();
                Console.WriteLine(ex.ToString());
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}

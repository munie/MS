using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace mnn.net.deprecated
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
        public delegate void MessageReceiverDelegate(byte[] data, AsyncState dealerState);
        public event MessageReceiverDelegate messageReceiver;

        private Socket sender;
        private byte[] readbuffer = new byte[8192];
        private int sendLength = 0;

        // Methods ================================================================

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

                // Event the one that needs to be evented.
                if (messageReceiver != null)
                    messageReceiver(null, AsyncState.ConnectSuccess);

                // start receive message
                Array.Clear(readbuffer, 0, readbuffer.Length);
                sender.BeginReceive(readbuffer, 0, readbuffer.Length, 0,
                    new AsyncCallback(ReceiveCallback), sender);
            }
            catch (Exception) {
                sender.Close();

                if (messageReceiver != null)
                    messageReceiver(null, AsyncState.ConnectFail);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try {
                Socket client = (Socket)ar.AsyncState;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead == 0) {
                    Close();
                    return;
                }

                if (messageReceiver != null)
                    messageReceiver(readbuffer.Take(bytesRead).ToArray(), AsyncState.ReadMessage);

                // restart the receiver
                sender.BeginReceive(readbuffer, 0, readbuffer.Length, 0,
                    new AsyncCallback(ReceiveCallback), sender);
            }
            catch (SocketException ex) {
                Close();
                Logger.WriteException(ex);
            }
            catch (Exception ex) {
                Logger.WriteException(ex);
            }
        }

        public void Close()
        {
            sender.Close();

            // Event the one that needs to be evented.
            if (messageReceiver != null)
                messageReceiver(null, AsyncState.Disconncted);
        }

        public void Send(byte[] data)
        {
            //sendLength = data.Length;
            //sender.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), sender);

            sender.Send(data, 0, data.Length, 0);
            if (messageReceiver != null)
                messageReceiver(data, AsyncState.SendSuccess);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);

                if (messageReceiver != null) {
                    if (bytesSent == sendLength)
                        messageReceiver(null, AsyncState.SendSuccess);
                    else
                        messageReceiver(null, AsyncState.SendFail);
                }
            }
            catch (Exception ex) {
                Logger.WriteException(ex);
            }
        }
    }
}

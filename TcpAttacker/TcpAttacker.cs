using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace TcpAttacker
{
    class TcpAttacker
    {
        private Socket socketSender;
        private System.Timers.Timer timer;
        private bool isConnected = false;

        public Encoding Coding;
        public Random Rand;
        public string Name;
        public string Protocol;
        public IPEndPoint EP;
        public double Interval;
        public List<string> MessageTable = new List<string>();

        public void Start()
        {
            timer = new System.Timers.Timer(Interval);

            timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
            {
                try {
                    if (isConnected == false) {
                        if (Protocol.ToLower() == "udp") {
                            socketSender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        }
                        else {
                            socketSender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            socketSender.Connect(EP);
                        }
                        isConnected = true;
                    }
                    else if (Rand.Next(0, 3) % 3 == 0) {
                        socketSender.Close();
                        isConnected = false;
                    }
                    else {
                        string data = MessageTable[Rand.Next(0, MessageTable.Count())];
                        if (Protocol.ToLower() == "udp")
                            socketSender.SendTo(Coding.GetBytes(data), EP);
                        else
                            socketSender.Send(Coding.GetBytes(data));
                    }
                }
                catch (Exception) { }
            });

            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            if (socketSender != null)
                socketSender.Close();
        }
    }
}

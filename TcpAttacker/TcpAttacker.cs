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

        public Random Rand;
        public string Name;
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
                        socketSender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socketSender.Connect(EP);
                        isConnected = true;
                    }
                    else if (Rand.Next(0, 3) % 3 == 0) {
                        socketSender.Close();
                        isConnected = false;
                    }
                    else {
                        string data = MessageTable[Rand.Next(0, MessageTable.Count())];
                        socketSender.Send(Encoding.Default.GetBytes(data));
                    }
                }
                catch (Exception) { }
            });

            timer.Start();
        }

        public void Stop()
        {
            timer.Close();
            if (socketSender != null)
                socketSender.Close();
        }
    }
}

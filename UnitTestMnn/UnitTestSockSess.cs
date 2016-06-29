using System;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using mnn.net;

namespace UnitTestMnn {
    [TestClass]
    public class UnitTestSockSess {
        [TestMethod]
        public void TestSockSessServer()
        {
            SockSessServer server = new SockSessServer();
            server.Listen(new IPEndPoint(0, 5964));
            server.accept_event += new SockSessServer.SockSessServerDelegate(AcceptEvent);
        }

        [TestMethod]
        public void TestSockSessClient()
        {
            SockSessClient client = new SockSessClient();
            client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3002));
            client.recv_event += new SockSessDelegate(RecvEvent);
            client.wfifo.Append(Encoding.UTF8.GetBytes("Hello SockSessClient"));
        }

        [TestMethod]
        public void TestRun()
        {
            while (true) Thread.Sleep(1000);
        }

        void AcceptEvent(object sender, SockSessAccept sess)
        {
            sess.recv_event += new SockSessDelegate(RecvEvent);
        }

        void RecvEvent(object sender)
        {
            SockSessBase sess = sender as SockSessBase;
            string msg = Encoding.UTF8.GetString(sess.rfifo.Take());
            Console.Write(msg);
        }
    }
}

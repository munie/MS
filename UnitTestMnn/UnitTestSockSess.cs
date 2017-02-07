using System;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using mnn.net;
using mnn.util;

namespace UnitTestMnn {
    [TestClass]
    public class UnitTestSockSess {
        [TestMethod]
        public void SockSessServerTest()
        {
            SockSessServer server = new SockSessServer();
            Loop.default_loop.Add(server);

            server.Bind(new IPEndPoint(0, 5964));
            server.Listen(100, OnAccept);

            Loop.default_loop.Run();
        }

        void OnAccept(SockSessServer server)
        {
            SockSess accept = server.Accept();
            accept.recv_event += new SockSess.SockSessDelegate(OnRecv);
            Loop.default_loop.Add(accept);
        }

        void OnRecv(SockSess sess)
        {
            string msg = Encoding.UTF8.GetString(sess.rfifo.Take());
            Console.Write(msg);
        }
    }
}

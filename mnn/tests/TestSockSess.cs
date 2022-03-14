using NUnit.Framework;
using System.Net;
using System.Text;
using mnn.net;
using mnn.glue;

namespace mnn.tests
{
	public class TestSockSess
	{
		[SetUp]
		public void Setup()
		{
		}

		[Test]
		public void Test1()
		{
			SockSessServer server = new SockSessServer();
			Loop.default_loop.Add(server);

			server.Bind(new IPEndPoint(0, 5964));
			server.Listen(100, OnAccept);

			Loop.default_loop.Run();
			Assert.Pass();
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

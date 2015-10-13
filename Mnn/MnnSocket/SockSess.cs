using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections;

namespace Mnn.MnnSocket
{
    public class SockSess
    {
        public Socket sock;
        public int type;
        public bool eof;
        public DateTime tick;

        public byte[] rdata;
        public UInt32 rdata_max = 8192, rdata_size, rdata_pos;
        public byte[] wdata;
        public UInt32 wdata_max = 8192, wdata_size;
        public object sdata;

        public delegate void RecvDelegate(SockSess sess);
        public delegate void SendDelegate(SockSess sess);
        public RecvDelegate recvfunc;
        public SendDelegate sendfunc;

        // Methods ============================================================================

        public SockSess(int type, Socket sock, RecvDelegate recv, SendDelegate send)
        {
            this.sock = sock;
            this.type = type;
            eof = false;
            tick = DateTime.Now;

            rdata = new byte[rdata_max];
            wdata = new byte[wdata_max];
            sdata = null;

            recvfunc = recv;
            sendfunc = send;
        }

        public static void Recv(SockSess sess)
        {
            try {
                sess.rdata_size = (UInt32)sess.sock.Receive(sess.rdata);
                sess.tick = DateTime.Now;
            }
            catch (Exception) {
                sess.eof = true;
                return;
            }

            if (sess.rdata_size == 0)
                sess.eof = true;
        }

        public static void Send(SockSess sess)
        {
            sess.sock.Send(sess.wdata.Take((int)sess.wdata_size).ToArray());
            sess.wdata_size = 0;
        }
    }

    public class SockSessManager
    {
        private TimeSpan stall_time;
        public List<SockSess> sess_table;

        public delegate void SessCreateDelegate(object sender, SockSess sess);
        public delegate void SessDeleteDelegate(object sender, SockSess sess);
        public delegate void SessParseDelegate(object sender, SockSess sess);
        public event SessCreateDelegate sess_create;
        public event SessDeleteDelegate sess_delete;
        public event SessParseDelegate sess_parse;

        // Methods ============================================================================

        public SockSessManager()
        {
            stall_time = new TimeSpan(TimeSpan.TicksPerMinute*60);
            sess_table = new List<SockSess>();
            sess_create = null;
            sess_delete = null;
            sess_parse = null;
        }

        public void Perform(int next)
        {
            // ** read
            var subset = from s in sess_table select s.sock;
            ArrayList list = new ArrayList(subset.ToArray());
            Socket.Select(list, null, null, next);
            foreach (var i in list) {
                foreach (var item in sess_table) {
                    if (item.sock == i) {
                        if (item.type == 0) {
                            Socket sock = item.sock.Accept();
                            sess_table.Add(new SockSess(2, sock, SockSess.Recv, SockSess.Send));
                            Console.WriteLine("[Info]: Session #A accepted to {0}.\n", sock.RemoteEndPoint.ToString());
                            if (sess_create != null)
                                sess_create(this, sess_table.Last());
                        }
                        else if (item.type == 1) {
                            item.recvfunc(item);
                        }
                        break;
                    }
                }
            }

            // ** timeout after read & parse & send & close
            list = new ArrayList(sess_table);
            foreach (SockSess item in list) {
                if (item.type != 0 && DateTime.Now.Subtract(item.tick) > stall_time)
                    item.eof = true;

                if (item.rdata_size != 0 && sess_parse != null)
                    sess_parse(this, item);

                if (item.wdata_size != 0)
                    item.sendfunc(item);

                if (item.eof == true) {
                    RemoveSession(item);
                    if (sess_delete != null)
                        sess_delete(this, item);
                }
            }
        }

        public void AddListenSession(IPEndPoint ep)
        {
            // Verify IPEndPoints
            IPEndPoint[] globalEPs = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (IPEndPoint globalEP in globalEPs) {
                if (ep.Equals(globalEP))
                    throw new ApplicationException(ep.ToString() + " is in listening.");
            }

            // Initialize the listenEP field of ListenerState
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Bind(ep);
            sock.Listen(100);
            sess_table.Add(new SockSess(0, sock, SockSess.Recv, SockSess.Send));
            Console.WriteLine("[info]: Session #L listened at {0}.\n", ep.ToString());
        }

        public void AddConnectSession(IPEndPoint ep)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try {
                sock.Connect(ep);
            }
            catch (Exception) {
                Console.WriteLine("[error]: Connected to {0} failed.\n", ep.ToString());
                return;
            }
            sess_table.Add(new SockSess(1, sock, SockSess.Recv, SockSess.Send));
            Console.WriteLine("[info]: Session #C connected to {0}.\n", ep.ToString());
        }

        private void RemoveSession(SockSess sess)
        {
            Console.WriteLine("[info]: Session #* deleted from {0}.\n", sess.sock.RemoteEndPoint.ToString());
            sess.sock.Shutdown(SocketShutdown.Both);
            sess.sock.Close();
            sess_table.Remove(sess);
        }
    }
}

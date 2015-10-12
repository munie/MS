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
        public UInt32 rdata_max = 4096, rdata_size, rdata_pos;
        public byte[] wdata;
        public UInt32 wdata_max = 2048, wdata_size;

        public object sdata;

        public delegate void RecvDelegate(SockSess sess);
        public delegate void SendDelegate(SockSess sess);
        public delegate void ParseDelegate(SockSess sess);
        public RecvDelegate recvfunc;
        public SendDelegate sendfunc;
        public ParseDelegate parsefunc;

        // Methods ============================================================================

        public SockSess(int type, Socket sock, RecvDelegate recv, SendDelegate send, ParseDelegate parse)
        {
            this.sock = sock;
            this.type = type;
            eof = false;
            tick = DateTime.Now;

            rdata = new byte[rdata_max];
            wdata = new byte[wdata_max];
            sdata = null;

            recvfunc += recv;
            sendfunc += send;
            parsefunc = parse;
        }

        public static void Recv(SockSess sess)
        {
            try {
                sess.rdata_size = (UInt32)sess.sock.Receive(sess.rdata);
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

        public static void Parse(SockSess sess)
        {
            sess.rdata_size = 0;
        }
    }

    public class SockSessManager
    {
        private SockSess.ParseDelegate parse_func;
        private TimeSpan stall_time;
        private List<SockSess> sess_table;

        public delegate void SessCreateDelegate(object sender, SockSess sess);
        public delegate void SessDeleteDelegate(object sender, SockSess sess);
        public event SessCreateDelegate sess_create;
        public event SessDeleteDelegate sess_delete;

        // Methods ============================================================================

        public SockSessManager(SockSess.ParseDelegate func)
        {
            parse_func = func;
            stall_time = new TimeSpan(TimeSpan.TicksPerMinute);
            sess_table = new List<SockSess>();
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
                            sess_table.Add(new SockSess(1, sock, SockSess.Recv, SockSess.Send, parse_func));
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

                if (item.rdata_size != 0)
                    item.parsefunc(item);

                if (item.wdata_size != 0)
                    item.sendfunc(item);

                if (item.eof == true) {
                    DeleteSession(item);
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
            sess_table.Add(new SockSess(0, sock, SockSess.Recv, SockSess.Send, SockSess.Parse));
        }

        private void DeleteSession(SockSess sess)
        {
            sess.sock.Shutdown(SocketShutdown.Both);
            sess.sock.Close();
            sess_table.Remove(sess);
        }
    }
}

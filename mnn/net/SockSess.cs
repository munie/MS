using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections;
using System.Threading;

namespace mnn.net {
    public enum SockType {
        listen = 0,
        accept = 2,
        connect = 1,
    }

    public class SockSess {
        public Socket sock;
        public SockType type;
        public IPEndPoint lep;
        public IPEndPoint rep;
        public bool eof;
        public DateTime tick;

        public byte[] rdata;
        public int rdata_max = 8192, rdata_size, rdata_pos;
        public byte[] wdata;
        public int wdata_max = 8192, wdata_size;
        public object sdata;

        public delegate void RecvDelegate(SockSess sess);
        public delegate void SendDelegate(SockSess sess);
        public RecvDelegate func_recv;
        public SendDelegate func_send;

        // Methods ============================================================================

        public SockSess(SockType type, Socket sock)
        {
            this.type = type;
            this.sock = sock;
            lep = sock.LocalEndPoint as IPEndPoint;
            rep = type == SockType.listen ? null : sock.RemoteEndPoint as IPEndPoint;
            eof = false;
            tick = DateTime.Now;

            rdata = new byte[rdata_max];
            wdata = new byte[wdata_max];
            this.sdata = null;

            func_recv = Recv;
            func_send = Send;
        }

        public static void Recv(SockSess sess)
        {
            try {
                if ((sess.rdata_size = sess.sock.Receive(sess.rdata)) == 0)
                    sess.eof = true;

                sess.tick = DateTime.Now;
            } catch (Exception) {
                sess.eof = true;
                return;
            }
        }

        public static void Send(SockSess sess)
        {
            sess.sock.Send(sess.wdata.Take(sess.wdata_size).ToArray());
            sess.wdata_size = 0;
        }

        public static SockSess Accept(SockSess sess)
        {
            try {
                Socket sock = sess.sock.Accept();
                return new SockSess(SockType.accept, sock);
            } catch (Exception) {
                return null;
            }
        }

        public static SockSess Listen(IPEndPoint ep)
        {
            // Verify IPEndPoints
            IPEndPoint[] globalEPs = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (IPEndPoint globalEP in globalEPs) {
                if (ep.Port == globalEP.Port)
                    return null;
            }

            // Initialize the listenEP field of ListenerState
            try {
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Bind(ep);
                sock.Listen(100);
                return new SockSess(SockType.listen, sock);
            } catch (Exception) {
                return null;
            }
        }

        public static SockSess Connect(IPEndPoint ep)
        {
            try {
                Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(ep);
                return new SockSess(SockType.connect, sock);
            } catch (Exception) {
                return null;
            }
        }
    }

    public class SessCenter {
        public List<SockSess> sess_table;
        private int stall_time;
        private Thread thread;

        public delegate void SessCreateDelegate(object sender, SockSess sess);
        public delegate void SessDeleteDelegate(object sender, SockSess sess);
        public delegate void SessParseDelegate(object sender, SockSess sess);
        public SessCreateDelegate sess_create;
        public SessDeleteDelegate sess_delete;
        public SessParseDelegate sess_parse;

        public SessCenter()
        {
            sess_table = new List<SockSess>();
            stall_time = 60 * 12;
            thread = null;
            sess_create = null;
            sess_delete = null;
            sess_parse = null;
        }

        // Methods ============================================================================

        public void Perform(int next)
        {
            ThreadCheck(true);

            // ** none
            if (sess_table.Count == 0) {
                System.Threading.Thread.Sleep(next);
                return;
            }

            // ** read
            var subset = from s in sess_table select s.sock;
            ArrayList list = new ArrayList(subset.ToArray());
            Socket.Select(list, null, null, next);
            foreach (var i in list) {
                foreach (var item in sess_table) {
                    if (item.sock == i) {
                        if (item.type == SockType.listen) {
                            SockSess retval = SockSess.Accept(item);
                            if (retval == null) continue;
                            AddSession(retval);
                        } else
                            item.func_recv(item);
                        break;
                    }
                }
            }

            // ** timeout after read & parse & send & close
            foreach (SockSess item in sess_table.ToArray()) {
                if (item.type == SockType.accept && DateTime.Now.Subtract(item.tick).TotalSeconds > stall_time)
                    item.eof = true;

                if (item.rdata_size != 0 && sess_parse != null)
                    sess_parse(this, item);

                if (item.wdata_size != 0)
                    item.func_send(item);

                if (item.eof == true) {
                    if (item.type == SockType.listen) {
                        foreach (var i in FindAcceptSession(item))
                            i.eof = true;
                    }
                    DeleteSession(item);
                }
            }
        }

        public SockSess MakeListen(IPEndPoint ep)
        {
            ThreadCheck(false);

            SockSess retval = SockSess.Listen(ep);
            if (retval == null) {
                Console.Write("[Error]: Listened to {0} failed.(alreay in listening)\n", ep.ToString());
                return null;
            }

            AddSession(retval);
            return retval;
        }

        public SockSess AddConnect(IPEndPoint ep)
        {
            ThreadCheck(false);

            SockSess retval = SockSess.Connect(ep);
            if (retval == null) {
                Console.Write("[Error]: Connected to {0} failed.\n", ep.ToString());
                return null;
            }

            AddSession(retval);
            return retval;
        }

        public void DelSession(SockSess sess)
        {
            ThreadCheck(false);

            sess.eof = true;
        }

        public void SendSession(SockSess sess, byte[] data)
        {
            ThreadCheck(false);

            try {
                if (sess.type == SockType.listen) {
                    foreach (var child in FindAcceptSession(sess))
                        child.sock.Send(data);
                } else {
                    sess.sock.Send(data);
                }
            } catch (Exception) { }
        }

        // Self Methods ========================================================================

        private void AddSession(SockSess sess)
        {
            sess_table.Add(sess);

            if (sess_create != null && sess.type != SockType.listen)
                sess_create(this, sess);

            if (sess.type == SockType.listen)
                Console.Write("[Info]: Session #L listened at {0}.\n", sess.lep.ToString());
            else if (sess.type == SockType.accept)
                Console.Write("[Info]: Session #A accepted to {0}.\n", sess.rep.ToString());
            else if (sess.type == SockType.connect)
                Console.Write("[Info]: Session #C connected to {0}.\n", sess.rep.ToString());
        }

        private void DeleteSession(SockSess sess)
        {
            if (sess_delete != null && sess.type != SockType.listen)
                sess_delete(this, sess);

            if (sess.type == SockType.listen)
                Console.Write("[Info]: Session #* deleted from {0}.\n", sess.lep.ToString());
            else {
                Console.Write("[Info]: Session #* deleted from {0}.\n", sess.rep.ToString());
                sess.sock.Shutdown(SocketShutdown.Both);
            }

            sess.sock.Close();
            sess_table.Remove(sess);
        }

        private SockSess[] FindAcceptSession(SockSess sess)
        {
            List<SockSess> retval = new List<SockSess>();

            if (sess.type == SockType.listen) {
                var subset = from s in sess_table
                             where s.type == SockType.accept && sess.lep.Port == s.lep.Port
                             select s;
                foreach (var item in subset)
                    retval.Add(item);
            }

            return retval.ToArray();
        }

        private void ThreadCheck(bool isSockThread)
        {
            if (isSockThread && thread == null)
                thread = Thread.CurrentThread;

            if (thread != null && thread != Thread.CurrentThread)
                throw new ApplicationException("Only socket thread can call this function!");
        }
    }
}

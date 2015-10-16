using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections;
using System.Threading;

namespace Mnn.MnnSock
{
    /// this file is converted from c proj, so still use c-style to name fileds

    public enum SockType
    {
        listen = 0,
        accept = 2,
        connect = 1,
    }

    public class SockSess
    {
        public SockType type;
        public Socket sock;
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
        public RecvDelegate recvfunc;
        public SendDelegate sendfunc;

        // Methods ============================================================================

        public SockSess(SockType type, Socket sock, RecvDelegate recv, SendDelegate send, object sdata)
        {
            this.type = type;
            this.sock = sock;
            lep = sock.LocalEndPoint as IPEndPoint;
            rep = type == SockType.listen ? null : sock.RemoteEndPoint as IPEndPoint;
            eof = false;
            tick = DateTime.Now;

            rdata = new byte[rdata_max];
            wdata = new byte[wdata_max];
            this.sdata = sdata;

            recvfunc = recv;
            sendfunc = send;
        }

        public static void Recv(SockSess sess)
        {
            try {
                sess.rdata_size = sess.sock.Receive(sess.rdata);
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
            sess.sock.Send(sess.wdata.Take(sess.wdata_size).ToArray());
            sess.wdata_size = 0;
        }
    }

    public class SockSessManager
    {
        public List<SockSess> sess_table;
        private TimeSpan stall_time;
        private Thread thread;

        public delegate void SessCreateDelegate(object sender, SockSess sess);
        public delegate void SessDeleteDelegate(object sender, SockSess sess);
        public delegate void SessParseDelegate(object sender, SockSess sess);
        public event SessCreateDelegate sess_create;
        public event SessDeleteDelegate sess_delete;
        public event SessParseDelegate sess_parse;

        // Methods ============================================================================

        public SockSessManager()
        {
            sess_table = new List<SockSess>();
            stall_time = new TimeSpan(TimeSpan.TicksPerMinute*12);
            thread = null;
            sess_create = null;
            sess_delete = null;
            sess_parse = null;
        }

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
                            Socket sock = item.sock.Accept();
                            sess_table.Add(new SockSess(SockType.accept, sock, SockSess.Recv, SockSess.Send, null));
                            Console.Write("[Info]: Session #A accepted to {0}.\n", sock.RemoteEndPoint.ToString());
                            if (sess_create != null)
                                sess_create(this, sess_table.Last());
                        }
                        else {
                            item.recvfunc(item);
                        }
                        break;
                    }
                }
            }

            // ** timeout after read & parse & send & close
            list = new ArrayList(sess_table);
            foreach (SockSess item in list) {
                if (item.type == SockType.accept && DateTime.Now.Subtract(item.tick) > stall_time)
                    item.eof = true;

                if (item.rdata_size != 0 && sess_parse != null)
                    sess_parse(this, item);

                if (item.wdata_size != 0)
                    item.sendfunc(item);

                if (item.eof == true) {
                    if (item.type == SockType.listen) {
                        foreach (var i in FindAcceptSession(item)) {
                            if (sess_delete != null)
                                sess_delete(this, i);
                            DeleteSession(i);
                        }
                    }
                    if (sess_delete != null)
                        sess_delete(this, item);
                    DeleteSession(item);
                }
            }
        }

        public Socket AddListenSession(IPEndPoint ep)
        {
            ThreadCheck(false);

            // Verify IPEndPoints
            IPEndPoint[] globalEPs = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (IPEndPoint globalEP in globalEPs) {
                //if (ep.Equals(globalEP)) {
                if (ep.Port == globalEP.Port) {
                    Console.Write("[error]: Listened to {0} failed.(alreay in listening)\n", ep.ToString());
                    return null;
                }
            }

            // Initialize the listenEP field of ListenerState
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Bind(ep);
            sock.Listen(100);
            sess_table.Add(new SockSess(0, sock, SockSess.Recv, SockSess.Send, null));
            Console.Write("[info]: Session #L listened at {0}.\n", ep.ToString());
            return sock;
        }

        public Socket AddConnectSession(IPEndPoint ep, object sdata = null)
        {
            ThreadCheck(false);

            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try {
                sock.Connect(ep);
            }
            catch (Exception) {
                Console.Write("[error]: Connected to {0} failed.\n", ep.ToString());
                return null;
            }

            sess_table.Add(new SockSess(SockType.connect, sock, SockSess.Recv, SockSess.Send, sdata));
            Console.Write("[info]: Session #C connected to {0}.\n", ep.ToString());
            if (sess_create != null)
                sess_create(this, sess_table.Last());
            return sock;
        }

        public void RemoveSession(Socket sock)
        {
            ThreadCheck(false);

            foreach (var item in sess_table) {
                if (item.sock == sock) {
                    item.eof = true;
                    break;
                }
            }
        }

        public void SendSession(Socket sock, byte[] data)
        {
            ThreadCheck(false);

            foreach (var item in sess_table) {
                if (item.type == SockType.listen && item.sock == sock) {
                    foreach (var child in FindAcceptSession(item))
                        child.sock.Send(data);
                    break;
                }
                else if (item.sock == sock) {
                    item.sock.Send(data);
                    break;
                }
            }
        }

        private void ThreadCheck(bool isSockThread)
        {
            if (isSockThread && thread == null)
                thread = Thread.CurrentThread;

            if (thread != null && thread != Thread.CurrentThread)
                throw new ApplicationException("Only socket thread can call this function!");
        }

        private void DeleteSession(SockSess sess)
        {
            if (sess.type == SockType.listen)
                Console.Write("[info]: Session #* deleted from {0}.\n", sess.lep.ToString());
            else if (sess.type != SockType.listen) {
                Console.Write("[info]: Session #* deleted from {0}.\n", sess.rep.ToString());
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
    }
}

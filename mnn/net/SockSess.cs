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
        accept = 1,
        connect = 2,
    }

    public class SockSess {
        public Socket sock { get; private set; }
        public SockType type { get; private set; }
        public IPEndPoint lep { get; private set; }
        public IPEndPoint rep { get; private set; }
        public bool eof { get; /*private*/ set; }
        public DateTime tick { get; private set; }

        private byte[] rdata;
        private int rdata_max = 8192;
        private int rdata_size;
        private int rdata_pos;
        private byte[] wdata;
        private int wdata_max = 8192;
        private int wdata_size;
        public object sdata;

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
        }

        public int RfifoRest()
        {
            return rdata_size - rdata_pos;
        }

        public void RfifoSkip(int len)
        {
            if (len > RfifoRest())
                rdata_pos = rdata_size;
            else
                rdata_pos += len;
        }

        public void RfifoFlush()
        {
            for (int i = 0; i < RfifoRest(); i++)
                rdata[i] = rdata[i + rdata_pos];

            rdata_size -= rdata_pos;
            rdata_pos = 0;
        }

        public byte[] RfifoTake()
        {
            byte[] retval = new byte[RfifoRest()];

            for (int i = 0; i < RfifoRest(); i++)
                retval[i] = rdata[rdata_pos + i];

            return retval;
        }

        public int WfifoRest()
        {
            return wdata_max - wdata_size;
        }

        public void WfifoSet(byte[] data)
        {
            if (data.Length > WfifoRest()) return;

            Array.Copy(data, 0, wdata, wdata_size, data.Length);
            wdata_size += data.Length;
        }

        public void Recv()
        {
            try {
                // flush if rdata's size is larger than rdata's max
                if (rdata_size > rdata_max >> 1)
                    RfifoFlush();

                // skip all if rdata's size is equal rdata's max
                if (rdata_size == rdata_max)
                    RfifoSkip(rdata_size);

                // recv to rdata
                int result = sock.Receive(rdata, rdata_size, rdata_max - rdata_size, SocketFlags.None);
                rdata_size += result;

                // set eof if received shutdown protocol
                if (result == 0)
                    eof = true;

                // update tick
                tick = DateTime.Now;
            } catch (Exception) {
                eof = true;
            }
        }

        public void Send()
        {
            if (wdata_size == 0) return;

            try {
                sock.Send(wdata, wdata_size, SocketFlags.None);
                wdata_size = 0;
            } catch (Exception) {
                eof = true;
            }
        }

        // Class Static ========================================================================

        public static SockSess Accept(SockSess sess)
        {
            try {
                Socket sock = sess.sock.Accept();
                sock.SendTimeout = 1000;
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

    public class SessCtl {
        private List<SockSess> sess_table;
        private int stall_time;
        private Thread thread;

        public delegate void SessDelegate(object sender, SockSess sess);
        public SessDelegate sess_create;
        public SessDelegate sess_delete;
        public SessDelegate sess_parse;

        private Dictionary<Delegate, object[]> dispatcher_delegate_table;

        public SessCtl()
        {
            sess_table = new List<SockSess>();
            stall_time = 60 * 24;
            thread = null;
            sess_create = null;
            sess_delete = null;
            sess_parse = null;
            dispatcher_delegate_table = new Dictionary<Delegate, object[]>();
        }

        // Methods ============================================================================

        public void Exec(int next)
        {
            ThreadCheck(true);

            if (dispatcher_delegate_table.Count != 0) {
                lock (dispatcher_delegate_table) {
                    foreach (var item in dispatcher_delegate_table)
                        item.Key.Method.Invoke(item.Key.Target, item.Value);
                    dispatcher_delegate_table.Clear();
                }
            }

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
                            item.Recv();
                        break;
                    }
                }
            }

            // ** timeout after read & parse & send & close
            foreach (SockSess item in sess_table.ToArray()) {
                if (item.type == SockType.accept && DateTime.Now.Subtract(item.tick).TotalSeconds > stall_time)
                    item.eof = true;

                if (item.RfifoRest() != 0 && sess_parse != null)
                    sess_parse(this, item);

                item.Send();

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
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(SessCtl));
                log.Warn(String.Format("Listened to {0} failed.(alreay in listening)", ep.ToString()));
                //Console.Write("[Warn]: Listened to {0} failed.(alreay in listening)\n", ep.ToString());
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
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(SessCtl));
                log.Warn(String.Format("Connected to {0} failed.", ep.ToString()));
                //Console.Write("[Warn]: Connected to {0} failed.\n", ep.ToString());
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

            if (sess.type == SockType.listen) {
                foreach (var child in FindAcceptSession(sess))
                    child.WfifoSet(data);
            } else
                sess.WfifoSet(data);

            //if (sess.type == SockType.listen) {
            //    foreach (var child in FindAcceptSession(sess)) {
            //        try {
            //            child.sock.Send(data);
            //        } catch (Exception ex) {
            //            log4net.ILog log = log4net.LogManager.GetLogger(typeof(SessCtl));
            //            log.Warn(String.Format("Send data to {0} failed.", child.rep.ToString()), ex);
            //            child.eof = true;
            //        }
            //    }
            //} else {
            //    try {
            //        sess.sock.Send(data);
            //    } catch (Exception ex) {
            //        log4net.ILog log = log4net.LogManager.GetLogger(typeof(SessCtl));
            //        log.Warn(String.Format("Send data to {0} failed.", sess.rep.ToString()), ex);
            //        sess.eof = true;
            //    }
            //}
        }

        public SockSess FindSession(SockType type, IPEndPoint lep, IPEndPoint rep)
        {
            ThreadCheck(false);

            SockSess retval = null;

            switch (type) {
                case SockType.listen:
                case SockType.connect:
                    foreach (var item in sess_table) {
                        if (item.type == type && item.lep.Equals(lep)) {
                            retval = item;
                            break;
                        }
                    }
                    break;
                case SockType.accept:
                    foreach (var item in sess_table) {
                        if (item.type == type && item.rep.Equals(rep)) {
                            retval = item;
                            break;
                        }
                    }
                    break;
                default:
                    break;
            }

            return retval;
        }

        public List<SockSess> GetSessionTable()
        {
            ThreadCheck(false);
            return new List<SockSess>(sess_table);
        }

        public void BeginInvoke(Delegate method, params object[] args)
        {
            lock (dispatcher_delegate_table) {
                dispatcher_delegate_table.Add(method, args);
            }
        }

        // Self Methods ========================================================================

        private void AddSession(SockSess sess)
        {
            sess_table.Add(sess);

            if (sess_create != null)
                sess_create(this, sess);

            log4net.ILog log = log4net.LogManager.GetLogger(typeof(SessCtl));
            if (sess.type == SockType.listen)
                log.Info(String.Format("Session #L listened at {0}.", sess.lep.ToString()));
                //Console.Write("[Info]: Session #A accepted to {0}.\n", sess.lep.ToString());
            else if (sess.type == SockType.accept)
                log.Info(String.Format("Session #A accepted to {0}.", sess.lep.ToString()));
                //Console.Write("[Info]: Session #A accepted to {0}.\n", sess.rep.ToString());
            else// if (sess.type == SockType.connect)
                log.Info(String.Format("Session #C connected to {0}.", sess.lep.ToString()));
                //Console.Write("[Info]: Session #C connected to {0}.\n", sess.rep.ToString());
        }

        private void DeleteSession(SockSess sess)
        {
            if (sess_delete != null)
                sess_delete(this, sess);

            log4net.ILog log = log4net.LogManager.GetLogger(typeof(SessCtl));
            if (sess.type == SockType.listen)
                log.Info(String.Format("Session #* deleted from {0}.", sess.lep.ToString()));
                //Console.Write("[Info]: Session #* deleted from {0}.\n", sess.lep.ToString());
            else {
                log.Info(String.Format("Session #* deleted from {0}.", sess.rep.ToString()));
                //Console.Write("[Info]: Session #* deleted from {0}.\n", sess.rep.ToString());
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

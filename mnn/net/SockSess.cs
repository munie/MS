using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using mnn.util;

namespace mnn.net {
    public class SockSess {
        private const int BASE_STALL = 60 * 12;

        protected Socket sock;
        private bool eof;
        private int stall;
        public DateTime tick;

        public string id { get; private set; }
        public DateTime conntime { get; private set; }

        public IPEndPoint lep { get { return (IPEndPoint)sock.LocalEndPoint; } }
        public IPEndPoint rep
        {
            get
            {
                try {
                    return (IPEndPoint)sock.RemoteEndPoint;
                } catch {
                    return null;
                }
            }
        }

        public Fifo<byte> rfifo { get; private set; }
        public Fifo<byte> wfifo { get; private set; }
        public object sdata { get; set; }

        public delegate void SockSessDelegate(object sender);
        public SockSessDelegate recv_event { get; set; }
        public SockSessDelegate close_event { get; set; }

        public SockSess()
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        public SockSess(Socket sock, int stall = BASE_STALL)
        {
            this.sock = sock;
            this.eof = false;
            this.stall = stall;
            this.tick = DateTime.Now;

            this.id = Guid.NewGuid().ToString();
            this.conntime = tick;

            rfifo = new Fifo<byte>();
            wfifo = new Fifo<byte>();
            sdata = null;

            recv_event = null;
            close_event = null;
        }

        private void Recv()
        {
            try {
                byte[] buffer = new byte[rfifo.FreeSpace()];
                int recvBytesAmount = sock.Receive(buffer, buffer.Length, SocketFlags.None);

                if (recvBytesAmount == 0)
                    eof = true;
                else {
                    rfifo.Append(buffer, recvBytesAmount);
                    if (recv_event != null)
                        recv_event(this);
                }
            } catch {
                eof = true;
            }
        }

        private void Send()
        {
            if (wfifo.Size() == 0) return;

            try {
                sock.Send(wfifo.Take(), SocketFlags.None);
            } catch (Exception ex) {
                log4net.LogManager.GetLogger("").Info("", ex);
                eof = true;
            }
        }

        private void RealClose()
        {
            if (close_event != null)
                close_event(this);
            try {
                sock.Shutdown(SocketShutdown.Both);
            } catch (Exception) { }
            sock.Close();
        }

        public void Close()
        {
            eof = true;
        }

        public bool IsClosed()
        {
            return eof;
        }

        private void MakeAlive()
        {
            this.tick = DateTime.Now;
        }

        public bool IsAlive()
        {
            if (DateTime.Now.Subtract(tick).TotalSeconds > stall)
                return false;
            else
                return true;
        }

        public virtual void DoSocket(int next)
        {
            // recv
            if (sock.Poll(next, SelectMode.SelectRead)) {
                this.MakeAlive();
                this.Recv();
            }

            // send
            if (sock.Poll(next, SelectMode.SelectWrite)) {
                this.MakeAlive();
                this.Send();
            }

            // close
            if (!IsAlive() || IsClosed())
                this.RealClose();
        }
    }

    public class SockSessServer : SockSess {
        public List<SockSessAccept> childs { get; private set; }
        public delegate void SockSessServerDelegate(object sender, SockSessAccept sess);
        public SockSessServerDelegate accept_event { get; set; }

        public SockSessServer()
        {
            childs = new List<SockSessAccept>();
            accept_event = null;
        }

        public void Listen(IPEndPoint ep)
        {
            if (!VerifyEndPointsValid(ep))
                throw new Exception("Specified ep is in using by another application...");

            sock.Bind(ep);
            sock.Listen(100);
        }

        public void Broadcast(byte[] msg)
        {
            foreach (var item in childs)
                item.wfifo.Append(msg);
        }

        public override void DoSocket(int next)
        {
            // accept
            if (sock.Poll(next, SelectMode.SelectRead)) {
                // get socket
                Socket accept_sock = sock.Accept();
                accept_sock.SendTimeout = 1000;
                // init accept_sess
                SockSessAccept accept_sess = new SockSessAccept(accept_sock, this);
                accept_sess.close_event += new SockSessDelegate((s) => {
                    if (s as SockSessAccept != null)
                        childs.Remove(s as SockSessAccept);
                });
                childs.Add(accept_sess);
                // raise accept event
                if (accept_event != null)
                    accept_event(this, accept_sess);
            }

            // send
            if (wfifo.Size() != 0)
                wfifo.Take();

            // close
            if (IsClosed()) {
                if (close_event != null)
                    close_event(this);
                sock.Close();
            }
        }

        public static IPEndPoint FindFreeEndPoint(IPAddress ip, int PortStart)
        {
            for (int i = 0; i < 10; i++) {
                int port = PortStart + new Random().Next() % (65535 - PortStart);
                IPEndPoint ep = new IPEndPoint(ip, port);
                if (VerifyEndPointsValid(ep))
                    return ep;
            }
            return null;
        }

        public static bool VerifyEndPointsValid(IPEndPoint ep)
        {
            IPEndPoint[] globalEPs = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (IPEndPoint globalEP in globalEPs) {
                if (ep.Port == globalEP.Port)
                    return false;
            }
            return true;
        }
    }

    public class SockSessAccept : SockSess {
        public SockSessServer parent { get; private set; }

        public SockSessAccept(Socket sock, SockSessServer parent)
            : base(sock)
        {
            this.parent = parent;
        }
    }

    public class SockSessClient : SockSess {
        private byte[] KeepAliveTime
        {
            get
            {
                uint dummy = 0;
                byte[] inOptionValues = new byte[Marshal.SizeOf(dummy) * 3];
                BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
                BitConverter.GetBytes((uint)5000).CopyTo(inOptionValues, Marshal.SizeOf(dummy));
                BitConverter.GetBytes((uint)5000).CopyTo(inOptionValues, Marshal.SizeOf(dummy) * 2);
                return inOptionValues;
            }
        }

        public SockSessClient()
            : base(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), int.MaxValue)
        {
        }

        public void Connect(IPEndPoint ep)
        {
            sock.Connect(ep);
            sock.IOControl(IOControlCode.KeepAliveValues, KeepAliveTime, null);
        }
    }
}

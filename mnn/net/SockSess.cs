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
    public class SockSess : IExecable {
        private const int BASE_STALL = 60 * 12;

        protected Socket sock;
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

        private bool eof;
        private int stall;
        public DateTime tick { get; set; }
        public DateTime conntime { get; private set; }
        public string id { get; private set; }

        public Fifo<byte> rfifo { get; private set; }
        public Fifo<byte> wfifo { get; private set; }
        public object sdata { get; set; }

        public delegate void SockSessDelegate(SockSess sess);
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

        public void DoExec()
        {
            DoSocket(1000);
        }
    }

    public class SockSessServer : SockSess {
        public delegate void AcceptDelegate(SockSessServer sever);
        public AcceptDelegate accept_event { get; set; }

        public SockSessServer()
        {
            accept_event = null;
        }

        public void Bind(IPEndPoint ep)
        {
            if (!VerifyEndPointsValid(ep))
                throw new Exception("Specified ep is in using by another application...");

            sock.Bind(ep);
        }

        public void Listen(int backlog, AcceptDelegate on_accept)
        {
            sock.Listen(backlog);
            accept_event += on_accept;
        }

        public SockSess Accept()
        {
            // get socket
            Socket accept_sock = sock.Accept();
            accept_sock.SendTimeout = 1000;
            // init accept_sess
            return new SockSess(accept_sock);
        }

        public override void DoSocket(int next)
        {
            // accept
            if (sock.Poll(next, SelectMode.SelectRead)) {
                if (accept_event != null)
                    accept_event(this);
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
        public delegate void ConnectDelegate(SockSessClient sess, int status, string strerr);

        public SockSessClient()
            : base(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), int.MaxValue)
        {
        }

        public void Connect(IPEndPoint ep)
        {
            sock.Connect(ep);
            sock.IOControl(IOControlCode.KeepAliveValues, KeepAliveTime, null);
        }

        //public void Connect(IPEndPoint ep, ConnectDelegate on_connect)
        //{
        //    int status = 0;
        //    string strerr = "";

        //    try {
        //        sock.Connect(ep);
        //        sock.IOControl(IOControlCode.KeepAliveValues, KeepAliveTime, null);
        //    } catch (Exception ex) {
        //        status = -1;
        //        strerr = ex.ToString();
        //    }

        //    if (on_connect != null)
        //        on_connect(this, status, strerr);
        //}
    }
}

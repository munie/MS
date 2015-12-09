using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace mnn.net {
    public class SockClientTcp {
        private Socket sock;
        private IPEndPoint toep;

        public SockClientTcp(IPEndPoint toep)
        {
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.toep = toep;
        }

        private static bool IsSocketConnected(Socket client)
        {
            bool blockingState = client.Blocking;
            try {
                byte[] tmp = new byte[1];
                client.Blocking = false;
                client.Send(tmp, 0, 0);
                return true;
            } catch (SocketException e) {
                // 产生 10035 == WSAEWOULDBLOCK 错误，说明被阻止了，但是还是连接的
                if (e.NativeErrorCode.Equals(10035))
                    return true;
                else
                    return false;
            } finally {
                client.Blocking = blockingState;    // 恢复状态
            }
        }

        public Socket GetSocket()
        {
            return sock;
        }

        public void Send(byte[] buffer)
        {
            if (!IsSocketConnected(sock))
                sock.Connect(toep);
            sock.Send(buffer);
        }

        public void SendEncryptUrl(string url)
        {
            url = EncryptSym.AESEncrypt(url);
            byte[] buffer = Encoding.UTF8.GetBytes(url);
            buffer = new byte[] { 0x01, 0x0C, (byte)(0x04 + buffer.Length & 0xff), (byte)(0x04 + buffer.Length >> 8 & 0xff) }
                .Concat(buffer).ToArray();

            if (!IsSocketConnected(sock))
                sock.Connect(toep);
            sock.Send(buffer);
        }
    }
}

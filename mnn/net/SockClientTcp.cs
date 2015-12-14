﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace mnn.net {
    public delegate void SockClientTcpSendCallback(string s);

    public class SockClientTcp {
        private Socket sock;
        private IPEndPoint toep;
        private Mutex mutex;

        public SockClientTcp(IPEndPoint toep)
        {
            sock = null;
            this.toep = toep;
            mutex = new Mutex(false);
        }

        private static bool IsSocketConnected(Socket client)
        {
            if (client == null) return false;

            try {
                // test writing
                if (!client.Poll(0, SelectMode.SelectWrite))
                    return false;
                else if (client.Poll(0, SelectMode.SelectRead)) {
                    byte[] tmp = new byte[1];
                    int nread = client.Receive(tmp, 1, SocketFlags.Peek);
                    if (nread == 0)
                        return false;
                }
            } catch (Exception) {
                return false;
            }

            return true;

            //bool blockingState = client.Blocking;
            //try {
            //    byte[] tmp = new byte[1];
            //    client.Blocking = false;
            //    client.Send(tmp, 0, 0);
            //    return true;
            //} catch (SocketException e) {
            //    // 产生 10035 == WSAEWOULDBLOCK 错误，说明被阻止了，但是还是连接的
            //    if (e.NativeErrorCode.Equals(10035))
            //        return true;
            //    else
            //        return false;
            //} finally {
            //    client.Blocking = blockingState;    // 恢复状态
            //}
        }

        public void Send(byte[] buffer, SockClientTcpSendCallback method)
        {
            try {
                if (!IsSocketConnected(sock)) {
                    if (sock != null) sock.Close();
                    sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    sock.Connect(toep);
                }
            } catch (Exception ex) {
                log4net.ILog log = log4net.LogManager.GetLogger(typeof(SockClientTcp));
                log.Error(String.Format("Connect to {0} failed.", toep.ToString()), ex);
            }


            ThreadPool.QueueUserWorkItem((s) =>
            {
                string retval = "";
                mutex.WaitOne();
                try {
                    byte[] tmp = new byte[1024];
                    if (sock.Poll(0, SelectMode.SelectRead))
                        sock.Receive(tmp);
                    sock.Send(buffer);
                    if (method != null && sock.Poll(3000, SelectMode.SelectRead)) {
                        int nread = sock.Receive(tmp);
                        if (nread > 0)
                            retval = Encoding.UTF8.GetString(tmp.Take(nread).ToArray());
                    }
                } catch (Exception ex) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(SockClientTcp));
                    log.Error("Exception in critical section.", ex);
                    return;
                }
                finally {
                    mutex.ReleaseMutex();
                }

                try {
                    if (method != null)
                        method.Method.Invoke(method.Target, new object[] { retval });
                } catch (Exception ex) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(SockClientTcp));
                    log.Error("Exception of invoking anonymous method.", ex);
                }
            });
        }

        public void SendEncryptUrl(string url, SockClientTcpSendCallback method)
        {
            url = EncryptSym.AESEncrypt(url);
            if (url == null) return;
            byte[] buffer = Encoding.UTF8.GetBytes(url);
            SockConvert.InsertSockHeader(SockRequestType.url, ref buffer);

            this.Send(buffer, method);
        }
    }
}
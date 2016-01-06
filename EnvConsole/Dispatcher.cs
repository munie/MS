using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using mnn.net;
using mnn.net.ctlcenter;

namespace EnvConsole
{
    class Dispatcher : DispatcherBase
    {
        private Queue<object[]> pack_queue;
        private SessCtl sessctl;

        public Dispatcher(SessCtl sessctl)
            : base()
        {
            pack_queue = new Queue<object[]>();
            this.sessctl = sessctl;
            Thread thread = new Thread(() =>
            {
                while (true) {
                    try {
                        object[] pack = null;
                        lock (pack_queue) {
                            if (pack_queue.Count != 0) // Any() is not thread safe
                                pack = pack_queue.Dequeue();
                        }
                        if (pack == null) {
                            Thread.Sleep(1000);
                            continue;
                        }

                        SockRequest request = pack[0] as SockRequest;
                        SockResponse response = pack[1] as SockResponse;
                        base.handle(request, ref response);
                        if (response.data != null && response.data.Length != 0) {
                            sessctl.BeginInvoke(new Action(() =>
                            {
                                SockSess result = sessctl.FindSession(SockType.accept, null, request.rep);
                                if (result != null)
                                    sessctl.SendSession(result, response.data);
                            }));
                        }
                    } catch (Exception ex) {
                        log4net.ILog log = log4net.LogManager.GetLogger(typeof(Dispatcher));
                        log.Warn("Exception of handling request to modules.", ex);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public override void handle(SockRequest request, ref SockResponse response)
        {
            if (request.content_mode == SockRequestContentMode.none) {
                lock (pack_queue) {
                    if (pack_queue.Count > 2048) {
                        log4net.ILog log = log4net.LogManager.GetLogger(typeof(Dispatcher));
                        log.Fatal("pack_queue's count is larger than 2048!");
                        pack_queue.Clear();
                        return;
                    } else {
                        pack_queue.Enqueue(new object[] { request, response });
                        return;
                    }
                }
            } else {
                try {
                    byte[] result = Convert.FromBase64String(Encoding.UTF8.GetString(request.data));
                    result = EncryptSym.AESDecrypt(result);
                    if (result != null) request.SetData(result);
                } catch (Exception) { }
            }

            base.handle(request, ref response);
            //response.data = EncryptSym.AESEncrypt(response.data);
        }
    }
}

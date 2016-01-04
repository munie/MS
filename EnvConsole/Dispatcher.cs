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
                    if (!pack_queue.Any()) {
                        Thread.Sleep(1000);
                        continue;
                    }

                    SockRequest request = pack_queue.Peek()[0] as SockRequest;
                    SockResponse response = pack_queue.Peek()[1] as SockResponse;
                    pack_queue.Dequeue();
                    base.handle(request, ref response);
                    if (response.data != null && response.data.Length != 0) {
                        sessctl.BeginInvoke(new Action(() => {
                            SockSess result = sessctl.FindSession(SockType.accept, null, request.rep);
                            if (result != null)
                                sessctl.SendSession(result, response.data);
                        }));
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public override void handle(SockRequest request, ref SockResponse response)
        {
            if (request.has_header) {
                try {
                    request.data = Convert.FromBase64String(Encoding.UTF8.GetString(request.data));
                    request.data = EncryptSym.AESDecrypt(request.data);
                } catch (Exception ex) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(Dispatcher));
                    log.Error("Exception of decrypting request data", ex);
                    request.data = null;
                }
                if (request.data == null) return;
            } else {
                lock (pack_queue) {
                    pack_queue.Enqueue(new object[] { request, response });
                    return;
                }
            }

            base.handle(request, ref response);
            //response.data = EncryptSym.AESEncrypt(response.data);
        }
    }
}

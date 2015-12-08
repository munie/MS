using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.net;

namespace EnvConsole
{
    class Dispatcher : DispatcherBase
    {
        public override void handle(SockRequest request, SockResponse response)
        {
            if (request.type != SockRequestType.unknown) {
                try {
                    request.data = Convert.FromBase64String(Encoding.UTF8.GetString(request.data));
                    request.data = EncryptSym.AESDecrypt(request.data);
                } catch (Exception) {
                    request.data = null;
                }
                if (request.data == null) return;
            }

            base.handle(request, response);
        }
    }
}

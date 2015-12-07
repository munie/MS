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
                string result = EncryptSym.AESDecrypt(Encoding.UTF8.GetString(request.data));
                if (result != null)
                    request.data = Encoding.UTF8.GetBytes(result);
                else
                    return;
            }

            base.handle(request, response);
        }
    }
}

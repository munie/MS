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
                char[] tmp = Encoding.UTF8.GetChars(request.data);
                request.data = EncryptSym.AESDecrypt(Convert.FromBase64CharArray(tmp, 0, tmp.Length));
                if (request.data == null) return;
            }

            base.handle(request, response);
        }
    }
}

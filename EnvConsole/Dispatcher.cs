﻿using System;
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
            if (request.type != SockRequestType.none) {
                try {
                    request.data = Convert.FromBase64String(Encoding.UTF8.GetString(request.data));
                    request.data = EncryptSym.AESDecrypt(request.data);
                } catch (Exception ex) {
                    log4net.ILog log = log4net.LogManager.GetLogger(typeof(Dispatcher));
                    log.Error("Exception of decrypting request data", ex);
                    request.data = null;
                }
                if (request.data == null) return;
            }

            base.handle(request, response);
            //response.data = EncryptSym.AESEncrypt(response.data);
        }
    }
}
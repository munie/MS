using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.net;

namespace mnn.misc.env {
    public class SEnvHandler {
        public static readonly string HANDLE_MSG = "HandleMsg";
    }

    public interface IEnvHandler {
        void HandleMsg(SockRequest request, SockResponse response);
    }
}

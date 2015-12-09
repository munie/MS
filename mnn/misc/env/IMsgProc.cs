using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.net;

namespace mnn.misc.env {
    public class SMsgProc {
        public static readonly string HANDLE_MSG = "HandleMsg";
    }

    public interface IMsgProc {
        void HandleMsg(SockRequest request, SockResponse response);
    }
}

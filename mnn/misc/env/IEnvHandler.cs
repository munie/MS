using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.net;

namespace mnn.misc.env {
    public class SEnvHandler {
        public static readonly string DO_HANDLER = "DoHandler";
    }

    public interface IEnvHandler {
        void DoHandler(SockRequest request, SockResponse response);
    }
}

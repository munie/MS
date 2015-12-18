using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.net;

namespace mnn.misc.env {
    public class SEnvFilter {
        public static readonly string DO_FILTER = "DoFilter";
    }

    public interface IEnvFilter {
        bool DoFilter(SockRequest request, SockResponse response);
    }
}

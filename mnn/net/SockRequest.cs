using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.net {
    public class SockRequest {
        public IPEndPoint lep { get; set; }
        public IPEndPoint rep { get; set; }
        public byte[] data { get; set; }
    }
}

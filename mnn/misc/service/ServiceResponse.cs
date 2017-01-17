using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.misc.service {
    [Serializable]
    public class ServiceResponse {
        public byte[] data { get; set; }
    }
}

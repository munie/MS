using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.misc.service {
    [Serializable]
    public class ServiceResponse {
        public byte[] raw_data { get; set; }
        public BaseContent content { get; set; }
    }

    public class BaseContent {
        public string id { get; set; }
        public int errcode { get; set; }
        public string errmsg { get; set; }
    }
}

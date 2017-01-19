using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.service {
    [Serializable]
    public class ServiceResponse {
        public string id { get; set; }
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public object data { get; set; }
    }
}

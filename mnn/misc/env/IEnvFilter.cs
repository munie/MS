using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.misc.service;

namespace mnn.misc.env {
    public class SEnvFilter {
        public static readonly string DO_FILTER = "DoFilter";
    }

    public interface IEnvFilter {
        bool DoFilter(ServiceRequest request, ServiceResponse response);
    }
}

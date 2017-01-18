using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.misc.service;

namespace mnn.misc.env {
    public class IEnvHandlerSymbols {
        public static readonly string DO_HANDLER = "DoHandler";
    }

    public interface IEnvHandler {
        void DoHandler(ServiceRequest request, ServiceResponse response);
    }
}

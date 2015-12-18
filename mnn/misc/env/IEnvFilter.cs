using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.env {
    public class SEnvFilter {
        public static readonly string DoFilter = "DoFilter";
    }

    public interface IEnvFilter {
        string DoFilter(string msg);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.module {
    public class IModuleSymbols {
        public static readonly string INIT = "Init";
        public static readonly string FINAL = "Final";
    }

    public interface IModule {
        void Init();
        void Final();
    }
}

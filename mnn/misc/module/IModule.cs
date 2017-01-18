using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.module {
    public class IModuleSymbols {
        public static readonly string INIT = "Init";
        public static readonly string FINAL = "Final";
        public static readonly string GET_MODULE_ID = "GetModuleID";
    }

    public interface IModule {
        void Init();
        void Final();
        string GetModuleID();
    }
}

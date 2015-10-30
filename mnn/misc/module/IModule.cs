using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.module {
    public class SModule {
        public static readonly string FULL_NAME = "mnn.misc.module.IModule";
        public static readonly string INIT = "Init";
        public static readonly string FINAL = "Final";
        public static readonly string GET_MODULE_ID = "GetModuleID";
        public static readonly string GET_MODULE_INFO = "GetModuleInfo";
    }

    public interface IModule {
        void Init();
        void Final();
        string GetModuleID();
        // Not used, so return anything is all right, like "MMMMNNNNNNNNCCCC" ...
        string GetModuleInfo();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.module
{
    public class SModule 
    {
        public static readonly string FullName = "mnn.misc.module.IModule";
        public static readonly string Init = "Init";
        public static readonly string Final = "Final";
        public static readonly string GetModuleID = "GetModuleID";
        public static readonly string GetModuleInfo = "GetModuleInfo";
    }

    public interface IModule
    {
        void Init();

        void Final();

        string GetModuleID();

        string GetModuleInfo();
    }
}

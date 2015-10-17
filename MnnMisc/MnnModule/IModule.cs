using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnMisc.MnnModule
{
    public class SModule 
    {
        public static readonly string FullName = "Mnn.MnnMisc.MnnModule.IModule";
        public static readonly string Init = "Init";
        public static readonly string Final = "Final";
        public static readonly string GetModuleID = "GetModuleID";
        public static readonly string GetModuleType = "GetModuleType";
    }

    public interface IModule
    {
        void Init();

        void Final();

        string GetModuleID();

        UInt16 GetModuleType();
    }
}

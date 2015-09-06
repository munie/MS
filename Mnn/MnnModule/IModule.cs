using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnModule
{
    public interface IModule
    {
        void Init();

        void Final();

        string GetModuleID();
    }
}

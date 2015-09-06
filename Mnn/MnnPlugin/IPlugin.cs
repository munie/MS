using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnPlugin
{
    public interface IPlugin
    {
        void Init();

        void Final();

        string GetPluginID();
    }
}

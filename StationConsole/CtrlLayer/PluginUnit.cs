using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole.CtrlLayer
{
    public class PluginUnit
    {
        public virtual string ID { get; set; }
        public virtual string Name { get; set; }
        public virtual string FilePath { get; set; }
        public virtual string FileName { get; set; }
        public virtual string FileComment { get; set; }

        public Mnn.MnnPlugin.PluginItem Plugin { get; set; }
    }
}

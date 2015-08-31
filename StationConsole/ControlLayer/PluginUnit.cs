using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole.ControlLayer
{
    public class PluginUnit : MnnUnit
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }

        public Mnn.MnnPlugin.PluginItem Plugin { get; set; }
    }
}

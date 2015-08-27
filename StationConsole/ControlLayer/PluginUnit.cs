using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole
{
    public class PluginUnit
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }

        public Mnn.MnnPlugin.PluginItem Plugin { get; set; }
    }
}

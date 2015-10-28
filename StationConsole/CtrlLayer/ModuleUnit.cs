using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole.CtrlLayer
{
    public class ModuleUnit
    {
        public virtual string ID { get; set; }
        public virtual string Name { get; set; }

        public virtual string FilePath { get; set; }
        public virtual string FileName { get; set; }
        public virtual string FileComment { get; set; }

        public mnn.misc.module.ModuleItem Module { get; set; }
    }
}

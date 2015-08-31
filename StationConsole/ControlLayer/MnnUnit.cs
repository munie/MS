using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole.ControlLayer
{
    public enum MnnUnitSchema
    {
        Server,
        Client,
        Plugin,
    }

    public class MnnUnit
    {
        public virtual string ID { get; set; }
        public virtual string Name { get; set; }
        public virtual MnnUnitSchema Schema { get; set; }
    }
}

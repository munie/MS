using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnUnit
{
    public enum UnitSchema
    {
        Server,
        Client,
        Module,
    }

    public class Unit
    {
        public virtual string ID { get; set; }
        public virtual string Name { get; set; }
        public virtual UnitSchema Schema { get; set; }
    }
}

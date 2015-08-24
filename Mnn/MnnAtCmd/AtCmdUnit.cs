using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnAtCmd
{
    public enum AtCmdUnitSchema
    {
        MainWindow,
        DataHandle,
        ClientPoint,
    };

    [Serializable]
    public class AtCmdUnit
    {
        public AtCmdUnitSchema Schema;
        public string ID;
        public string Data;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnAtCmd
{
    public enum AtCmdUnitDirect
    {
        Request,
        Respond,
    };

    public enum AtCmdUnitSchema
    {
        MainWindow,
        DataHandle,
        ClientPoint,
    };

    public enum AtCmdUnitType
    {
        ClientConnect,
        ClientDisconn,
        ClientReadMsg,
        ClientSendMsg,
        ClientUpdate,
    }

    [Serializable]
    public class AtCmdUnit
    {
        public AtCmdUnitDirect Direct;
        public AtCmdUnitSchema Schema;
        public AtCmdUnitType Type;
        public string ID;
        public string Data;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole
{
    public enum AtCmdUnitDirect
    {
        Request,
        Respond,
    };

    public enum AtCmdUnitSchema
    {
        MainWindow,
        ServerUnit,
        ClientUnit,
    };

    public enum AtCmdUnitType
    {
        ClientClose,
        ClientUpdateID,
        ClientUpdateName,
        ClientSendMsg,
    }

    [Serializable]
    public class AtCmdUnit
    {
        public AtCmdUnitDirect Direct;
        public AtCmdUnitSchema Schema;
        public AtCmdUnitType Type;
        public string ID;
        public string ToID;
        public string ToEP;
        public string Data;
    }
}

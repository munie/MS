using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnMisc.MnnEnv
{
    public enum UnitSchema
    {
        Server,
        Client,
        Module,
    }

    public enum AtCommandDirect
    {
        Request,
        Respond,
    };

    public enum AtCommandDataType
    {
        ClientClose,
        ClientUpdateID,
        ClientUpdateName,
        ClientSendMsg,
    }

    [Serializable]
    public class AtCommand
    {
        public AtCommandDirect Direct;
        public string ID;
        public string FromID;
        public UnitSchema FromSchema;
        public string ToID;
        public UnitSchema ToSchema;
        public AtCommandDataType DataType;
        public string Data;
        public string Result;

        public string ToEP;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StationConsole.ControlLayer
{
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
        public MnnUnitSchema FromSchema;
        public string ToID;
        public MnnUnitSchema ToSchema;
        public AtCommandDataType DataType;
        public string Data;

        public string ToEP;
    }
}

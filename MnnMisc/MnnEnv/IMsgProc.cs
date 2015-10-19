using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnMisc.MnnEnv
{
    public class SMsgProc
    {
        public static readonly string FullName = "Mnn.MnnMisc.MnnEnv.IMsgProc";
        public static readonly string AppendMsg = "AppendMsg";
        public static readonly string HandleMsg = "HandleMsg";
        public static readonly string HandleMsgByte = "HandleMsgByte";
        public static readonly string AtCmdResult = "AtCmdResult";
    }

    public interface IMsgProc
    {
        void AppendMsg(System.Net.IPEndPoint ep, string msg);

        void HandleMsg(System.Net.IPEndPoint ep, string msg);

        void AtCmdResult(AtCommand atCmd);

    }
}

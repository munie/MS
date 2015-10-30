using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.env
{
    public class SMsgProc
    {
        public static readonly string FULL_NAME = "mnn.misc.env.IMsgProc";
        public static readonly string TRANSLATE = "Translate";
        public static readonly string ATCMD_RESULT = "AtCmdResult";
        public static readonly string APPEND_MSG = "AppendMsg";
        public static readonly string HANDLE_MSG = "HandleMsg";
    }

    public interface IMsgProc
    {
        string Translate(string msg);
        void AtCmdResult(AtCommand atCmd);
        void AppendMsg(System.Net.IPEndPoint ep, string msg);
        void HandleMsg(System.Net.IPEndPoint ep, string msg);
    }
}

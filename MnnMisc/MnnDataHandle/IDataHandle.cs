using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnMisc.MnnDataHandle
{
    public class SDataHandle
    {
        public static readonly string FullName = "Mnn.MnnMisc.MnnDataHandle.IDataHandle";
        public static readonly string AppendMsg = "AppendMsg";
        public static readonly string HandleMsg = "HandleMsg";
        public static readonly string HandleMsgByte = "HandleMsgByte";
        public static readonly string AtCmdResult = "AtCmdResult";
    }

    public interface IDataHandle
    {
        void AppendMsg(System.Net.IPEndPoint ep, string msg);

        void HandleMsg(System.Net.IPEndPoint ep, string msg);

        void HandleMsgByte(System.Net.IPEndPoint ep, byte[] msg);

        void AtCmdResult(AtCommand atCmd);

    }
}

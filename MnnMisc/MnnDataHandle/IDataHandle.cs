using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnMisc.MnnDataHandle
{
    public interface IDataHandle
    {
        void AppendMsg(System.Net.IPEndPoint ep, string msg);

        void HandleMsg(System.Net.IPEndPoint ep, string msg);

        void HandleMsgByte(System.Net.IPEndPoint ep, byte[] msg);

        void AtCmdResult(AtCommand atCmd);

    }
}

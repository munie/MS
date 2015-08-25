using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Mnn.MnnAtCmd
{
    public class AtCmdServer
    {
        //private AtCmdPipeServer pipeServer = new AtCmdPipeServer();
        public AtCmdSockServer sockServer = new AtCmdSockServer();

        public void Run(ExecuteAtCmdDeleagte execCommandCallback)
        {
            sockServer.ExecCommand += execCommandCallback;
            sockServer.Run(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3005));
        }
    }
}

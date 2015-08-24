using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mnn.MnnAtCmd
{
    public class AtCmdServer
    {
        private AtCmdPipeServer pipeServer = new AtCmdPipeServer();
        private AtCmdSockServer sockServer = new AtCmdSockServer();

        public void Run(string pipeName, System.Net.IPEndPoint ep, ExecuteAtCmdDeleagte execCommandCallback)
        {
            pipeServer.ExecCommand += execCommandCallback;
            pipeServer.Run(pipeName);

            sockServer.ExecCommand += execCommandCallback;
            sockServer.Run(ep);
        }
    }
}

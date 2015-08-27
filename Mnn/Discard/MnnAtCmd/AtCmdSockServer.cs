using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Xml.Serialization;

namespace Mnn.MnnAtCmd
{
    public class AtCmdSockServer<T>
        where T : MnnSocket.SockServer, new()
    {
        public event ExecuteAtCmdDeleagte ExecCommand;

        private T sockServer;

        public void Run(IPEndPoint ep)
        {
            sockServer = new T();

            sockServer.Start(ep);
            sockServer.ClientReadMsg += Client_ReadMsg;
        }

        private void Client_ReadMsg(object sender, Mnn.MnnSocket.ClientEventArgs e)
        {
            if (ExecCommand == null)
                return;

            try {
                MemoryStream memory = new MemoryStream(e.Data);
                XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCmdUnit));
                AtCmdUnit atCmdUnit = xmlFormat.Deserialize(memory) as AtCmdUnit;
                memory.Close();

                ExecCommand(atCmdUnit);
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
            }
        }
    }
}

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
    public class AtCmdSockServer : Mnn.MnnSocket.TcpServer
    {
        public event ExecuteAtCmdDeleagte ExecCommand;

        public void Run(IPEndPoint ep)
        {
            this.Start(ep);
            this.ClientReadMsg += Client_ReadMsg;
        }

        private void Client_ReadMsg(object sender, Mnn.MnnSocket.ClientEventArgs e)
        {
            if (ExecCommand != null) {
                try {
                    MemoryStream memory = new MemoryStream(e.Data);
                    XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCmdUnit));
                    AtCmdUnit cmdUnit = xmlFormat.Deserialize(memory) as AtCmdUnit;

                    this.Send(e.RemoteEP, ExecCommand(cmdUnit));
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}

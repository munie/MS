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
            if (ExecCommand == null)
                return;

            try {
                if (e.Data.First() == '|') {
                    string msg = Encoding.ASCII.GetString(e.Data);
                    string[] msgs = msg.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    
                    Dictionary<string, string> dc = new Dictionary<string, string>();
                    foreach (var item in msgs) {
                        string[] tmp = item.Split("=".ToCharArray());
                        dc.Add(tmp[0], tmp[1]);
                    }

                    AtCmdUnit atCmdUnit = new AtCmdUnit();
                    atCmdUnit.Schema = AtCmdUnitSchema.ClientPoint;
                    atCmdUnit.ID = dc["CCID"];
                    atCmdUnit.Data = dc["mlstr"];

                    if (!ExecCommand(atCmdUnit)) {
                        string ret = "|HT=0|CCID=898602A5111356056659|error=offline";
                        this.Send(e.RemoteEP, Encoding.ASCII.GetBytes(ret));
                    }

                    return;
                }
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
            }
            /*
            try {
                MemoryStream memory = new MemoryStream(e.Data);
                XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCmdUnit));
                AtCmdUnit atCmdUnit = xmlFormat.Deserialize(memory) as AtCmdUnit;

                ExecCommand(atCmdUnit);
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
            }
             * */
        }
    }
}

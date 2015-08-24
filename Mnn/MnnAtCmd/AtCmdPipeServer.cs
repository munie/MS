using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using System.Xml.Serialization;

namespace Mnn.MnnAtCmd
{
    public class AtCmdPipeServer
    {
        public event ExecuteAtCmdDeleagte ExecCommand;

        private string PipeName;

        // Method ====================================================================================

        public void Run(string pipeName)
        {
            PipeName = pipeName;

            Thread thread = new Thread(() =>
            {
                try {
                    NamedPipeServerStream pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, -1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    pipeServer.BeginWaitForConnection(ReadCallback, pipeServer);
                }
                catch (Exception ex) {
                    Mnn.MnnUtil.Logger.WriteException(ex);
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        private void ReadCallback(IAsyncResult ar)
        {
            NamedPipeServerStream pipeServerStream = ar.AsyncState as NamedPipeServerStream;

            try {
                pipeServerStream.EndWaitForConnection(ar);

                // 监听下一个PipeClient
                NamedPipeServerStream pipeServerStreamNext = new NamedPipeServerStream(PipeName, PipeDirection.InOut, -1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                pipeServerStreamNext.BeginWaitForConnection(ReadCallback, pipeServerStreamNext);

                byte[] readbuffer = new byte[1024];
                MemoryStream memory = new MemoryStream(readbuffer);
                XmlSerializer xmlFormat = new XmlSerializer(typeof(AtCmdUnit));
                while (true) {
                    try {
                        pipeServerStream.Read(readbuffer, 0, readbuffer.Length);
                        AtCmdUnit cmdUnit = xmlFormat.Deserialize(memory) as AtCmdUnit;

                        if (ExecCommand != null) {
                            byte[] retval = ExecCommand(cmdUnit);
                            pipeServerStream.Write(retval, 0, retval.Length);
                        }
                        else
                            pipeServerStream.WriteByte(0);
                    }
                    catch (InvalidOperationException ex) {
                        // From XmlSerializer when xml syntax is wrong
                        Mnn.MnnUtil.Logger.WriteException(ex);
                        break;
                    }
                    catch (IOException) {
                        break;
                    }
                    catch (Exception ex) {
                        Mnn.MnnUtil.Logger.WriteException(ex);
                        break;
                    }
                }
            }
            catch (Exception ex) {
                Mnn.MnnUtil.Logger.WriteException(ex);
            }
        }
    }
}

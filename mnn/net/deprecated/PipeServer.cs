using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using System.Xml.Serialization;

namespace mnn.net.deprecated {
    public class PipeServer {
        public event EventHandler<ClientEventArgs> ClientReadMsg;

        private string pipeName;
        private int readbufferSize = 4096;
        private bool isExitThread = false;

        // Method ====================================================================================

        public void Start(string name)
        {
            pipeName = name;

            isExitThread = false;
            Thread thread = new Thread(() =>
            {
                while (true) {
                    if (isExitThread == true) {
                        isExitThread = false;
                        break;
                    }

                    try {
                        // xp 无法使用pipe，所以加点延迟预防一下...
                        Thread.Sleep(500);
                        NamedPipeServerStream pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, -1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        pipeServer.BeginWaitForConnection(ReadCallback, pipeServer);
                    } catch (Exception ex) {
                        Logger.WriteException(ex);
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            isExitThread = true;
        }

        private void ReadCallback(IAsyncResult ar)
        {
            NamedPipeServerStream pipeServerStream = ar.AsyncState as NamedPipeServerStream;

            try {
                pipeServerStream.EndWaitForConnection(ar);

                byte[] readbuffer = new byte[readbufferSize];
                while (true) {
                    try {
                        int bytesRead = pipeServerStream.Read(readbuffer, 0, readbuffer.Length);

                        if (ClientReadMsg != null)
                            ClientReadMsg(this, new ClientEventArgs(null, null, readbuffer.Take(bytesRead).ToArray()));
                    } catch (InvalidOperationException ex) {
                        // From XmlSerializer when xml syntax is wrong
                        Logger.WriteException(ex);
                        break;
                    } catch (IOException) {
                        break;
                    } catch (Exception ex) {
                        Logger.WriteException(ex);
                        break;
                    }
                }
            } catch (Exception ex) {
                Logger.WriteException(ex);
            }
        }
    }
}

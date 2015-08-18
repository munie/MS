using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using Mnn.MnnUtil;
using Mnn.MnnPlugin;

namespace StationConsole
{
    partial class DataHandleUnit
    {
        class ClientData
        {
            public IPEndPoint ep { get; set; }
            public string data { get; set; }
        }
    }

    partial class DataHandleUnit
    {
        private Queue<ClientData> dataTable = new Queue<ClientData>();
        private Semaphore dataSem = new Semaphore(0, 200);
        private Thread dataThread;
        private bool isExitDataThread = false;

        public Mnn.MnnPlugin.PluginItem Plugin { get; private set; }
        public Mnn.MnnSocket.TcpServer Listener { get; set; }
        public System.Timers.Timer Timer { get; private set; }

        // Methods ============================================================================

        public void AppendData(IPEndPoint ep, string data)
        {
            if (dataThread == null || dataThread.ThreadState == ThreadState.Aborted ||
                dataThread.ThreadState == ThreadState.Stopped)
                return;

            lock (dataTable) {
                dataTable.Enqueue(new ClientData() { ep = ep, data = data });
            }
            dataSem.Release();
        }

        public void StartHandleData()
        {
            isExitDataThread = false;

            dataThread = new Thread(() =>
            {
                try {
                    while (true) {
                        if (isExitDataThread == true) {
                            isExitDataThread = false;
                            break;
                        }
                        if (dataSem.WaitOne(500) == false)
                            continue;

                        ClientData clientData;
                        lock (dataTable) {
                            clientData = dataTable.Peek();
                            dataTable.Dequeue();
                        }

                        try {
                            object retValue = Plugin.Invoke("IDataHandle", "Handle", new object[] { clientData.data });
                            if (retValue != null)
                                this.Listener.Send(clientData.ep, (string)retValue);
                        }
                        catch (Exception ex) {
                            Logger.WriteException(ex);
                        }
                    }
                }
                catch (Exception) { }
            });

            dataThread.IsBackground = true;
            dataThread.Start();
        }

        public void StopHandleData()
        {
            isExitDataThread = true;
            dataThread.Join();
        }

        public void InitializeSource()
        {
            this.Plugin = new PluginItem();
            this.Listener = new Mnn.MnnSocket.TcpServer();
            this.Timer = new System.Timers.Timer();
        }

        public int LoadDataHandlePlugin(string filePath)
        {
            int listenPort = 0;

            try {
                this.Plugin.Load(filePath);
            }
            catch (Exception) {
                throw;
            }

            try {
                listenPort = (int)this.Plugin.Invoke("IDataHandle", "GetDefaultListenPort", null);
            }
            catch (Exception) {
                this.Plugin.UnLoad();
                throw;
            }

            return listenPort;
        }

        public void UnloadDataHandlePlugin()
        {
            this.Plugin.UnLoad();
        }

        public void StartListener(IPEndPoint ep)
        {
            this.Listener.Start(ep);
        }

        public void StopListener()
        {
            // 逻辑上讲，不会出现异常
            this.Listener.Stop();
            // 同时关闭对应客户端
            //this.Listener.CloseClient();
        }

        public void StartTimerCommand(double interval, string command)
        {
            this.Timer.Interval = interval;
            this.Timer.Elapsed += new System.Timers.ElapsedEventHandler((s, ea) =>
            {
                try {
                    this.Listener.Send(command);
                }
                catch (Exception) { }
            });

            this.Timer.Start();
        }

        public void StopTimerCommand()
        {
            this.Timer.Stop();
        }
    }
}

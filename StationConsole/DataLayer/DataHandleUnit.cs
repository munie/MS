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
    public class DataHandleUnit
    {
        public Mnn.MnnPlugin.PluginItem Plugin = new PluginItem();

        // Methods ============================================================================

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

        public void InitializeSource()
        {
            this.Plugin.Invoke("IDataHandle", "InitializeSource", null);
        }

        public void StartListener(IPEndPoint ep)
        {
            this.Plugin.Invoke("IDataHandle", "StartListener", new object[] { ep });
        }

        public void StopListener()
        {
            this.Plugin.Invoke("IDataHandle", "StopListener", null);
        }

        public void StartTimerCommand(double interval, string command)
        {
            this.Plugin.Invoke("IDataHandle", "StartTimerCommand", new object[] { interval, command });
        }

        public void StopTimerCommand()
        {
            this.Plugin.Invoke("IDataHandle", "StopTimerCommand", null);
        }

        public void SendClientCommand(IPEndPoint ep, byte[] data)
        {
            this.Plugin.Invoke("IDataHandle", "SendClientCommand", new object[] { ep, data });
        }

        public void CloseClient(IPEndPoint ep)
        {
            this.Plugin.Invoke("IDataHandle", "CloseClient", new object[] { ep });
        }
    }
}

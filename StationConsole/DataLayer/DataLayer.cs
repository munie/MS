using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Net;

namespace StationConsole
{
    class AtCmdServerConfig
    {
        public string Protocol { get; set; }
        public string IpAddress { get; set; }
        public string Port { get; set; }
        public string PipeName { get; set; }
    }

    class DataLayer
    {
        // From config.xml
        public Encoding coding;
        public IPAddress ipAddress;
        public List<AtCmdServerConfig> atCmdServerConfigTable;

        // Data Binging
        public ObservableCollection<DataHandlePlugin> dataHandlePluginTable;
        public ObservableCollection<ClientPointState> clientPointTable;
    }
}

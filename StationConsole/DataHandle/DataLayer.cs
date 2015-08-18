using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Net;

namespace StationConsole
{
    class DataLayer
    {
        public static IPAddress ipAddress;

        public static ObservableCollection<DataHandleState> dataHandleTable;
        public static ObservableCollection<ClientPointState> clientPointTable;

    }
}

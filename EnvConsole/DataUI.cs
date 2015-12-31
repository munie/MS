using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;
using System.Net;
using System.Reflection;
using mnn.net;
using EnvConsole.Unit;

namespace EnvConsole
{
    class DataUI : INotifyPropertyChanged
    {
        public ObservableCollection<ServerUnit> ServerTable { get; set; }
        public ObservableCollection<ClientUnit> ClientTable { get; set; }
        public ObservableCollection<ModuleUnit> ModuleTable { get; set; }
        public ReaderWriterLock RwlockModuleTable { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        // socket parse log
        public System.Windows.Controls.TextBox MsgBox { get; set; }
        //private StringBuilder log;
        //public string Log
        //{
        //    get { return log.ToString(); }
        //    set
        //    {
        //        if (log.Length >= 20 * 1024 || value == "")
        //            log.Clear();
        //        log.Append(value);
        //        if (PropertyChanged != null)
        //            PropertyChanged(this, new PropertyChangedEventArgs("Log"));
        //    }
        //}

        // currentAcceptCount
        private int currentAcceptCount;
        public int CurrentAcceptCount
        {
            get { return currentAcceptCount; }
            set
            {
                currentAcceptCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("CurrentAcceptCount"));
            }
        }

        // historyAcceptOpenCount
        private int historyAcceptOpenCount;
        public int HistoryAcceptOpenCount
        {
            get { return historyAcceptOpenCount; }
            set
            {
                historyAcceptOpenCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("HistoryAcceptOpenCount"));
            }
        }

        // historyAcceptCloseCount
        private int historyAcceptCloseCount;
        public int HistoryAcceptCloseCount
        {
            get { return historyAcceptCloseCount; }
            set
            {
                historyAcceptCloseCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("HistoryAcceptCloseCount"));
            }
        }

        // currentPackCount
        private int currentPackCount;
        public int CurrentPackCount
        {
            get { return currentPackCount; }
            set
            {
                currentPackCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("CurrentPackCount"));
            }
        }

        // historyPackFetchedCount
        private int historyPackFetchedCount;
        public int HistoryPackFetchedCount
        {
            get { return historyPackFetchedCount; }
            set
            {
                historyPackFetchedCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("HistoryPackFetchedCount"));
            }
        }

        // historyPackParsedCount
        private int historyPackParsedCount;
        public int HistoryPackParsedCount
        {
            get { return historyPackParsedCount; }
            set
            {
                historyPackParsedCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("HistoryPackParsedCount"));
            }
        }

        public DataUI()
        {
            ServerTable = new ObservableCollection<ServerUnit>();
            ClientTable = new ObservableCollection<ClientUnit>();
            ModuleTable = new ObservableCollection<ModuleUnit>();
            RwlockModuleTable = new ReaderWriterLock();

            //log = new StringBuilder();
            currentAcceptCount = 0;
            historyAcceptOpenCount = 0;
            historyAcceptCloseCount = 0;
            currentPackCount = 0;
            historyPackFetchedCount = 0;
            historyPackParsedCount = 0;
        }

        public void ServerStart(string ip, int port)
        {
            foreach (var item in ServerTable) {
                if (item.IpAddress.Equals(ip) && item.Port == port) {
                    item.ListenState = ServerUnit.ListenStateStarted;
                    break;
                }
            }
        }

        public void ServerStop(string ip, int port)
        {
            foreach (var item in ServerTable) {
                if (item.IpAddress.Equals(ip) && item.Port == port) {
                    item.ListenState = ServerUnit.TimerStateStoped;
                    break;
                }
            }
        }

        public void ClientAdd(IPEndPoint lep, IPEndPoint rep)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ClientUnit client = new ClientUnit();
                client.ID = "";
                client.Name = "";
                client.RemoteEP = rep;
                foreach (var item in ServerTable) {
                    if (item.Port.Equals(lep.Port)) {
                        client.ServerID = item.ID;
                        client.ServerName = item.Name;
                        break;
                    }
                }
                client.ConnectTime = DateTime.Now;

                ClientTable.Add(client);
                CurrentAcceptCount++;
                HistoryAcceptOpenCount++;
            }));
        }

        public void ClientDel(IPEndPoint rep)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var subset = from s in ClientTable where s.RemoteEP.Equals(rep) select s;
                if (!subset.Any())
                    return;

                ClientTable.Remove(subset.First());
                CurrentAcceptCount--;
                HistoryAcceptCloseCount++;
            }));
        }

        public void ClientUpdate(IPEndPoint rep, string fieldName, object value)
        {
            Type t = typeof(ClientUnit);
            PropertyInfo propertyInfo = t.GetProperty(fieldName);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var subset = from s in this.ClientTable
                             where s.RemoteEP.Equals(rep)
                             select s;

                if (subset.Any())
                    propertyInfo.SetValue(subset.First(), value, null);
            }));
        }

        public void Logger(string log)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                //Log = log;
                if (MsgBox.Text.Length >= 20 * 1024)
                    MsgBox.Clear();

                MsgBox.AppendText(log);
                MsgBox.ScrollToEnd();
            }));
        }

        public void PackRecved()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                CurrentPackCount++;
                HistoryPackFetchedCount++;
            }));
        }

        public void PackParsed()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                CurrentPackCount--;
                HistoryPackParsedCount++;
            }));
        }
    }
}

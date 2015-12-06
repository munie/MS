using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;
using mnn.net;

namespace SockMaster {
    class DataUI : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        // socket information
        public ObservableCollection<SockUnit> SockTable { get; set; }

        // socket parse log
        private StringBuilder log;
        public string Log
        {
            get { return log.ToString(); }
            set
            {
                if (log.Length >= 20 * 1024 || value == "")
                    log.Clear();
                log.Append(value);
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Log"));
            }
        }

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

        public DataUI()
        {
            SockTable = new ObservableCollection<SockUnit>();
            log = new StringBuilder();
            currentAcceptCount = 0;
            historyAcceptOpenCount = 0;
            historyAcceptCloseCount = 0;
        }

        public void SockAdd(SockSess sess)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // update SockTable
                if (sess.type == SockType.accept) {
                    var subset = from s in SockTable
                                 where s.Type == SockType.listen && s.EP.Port == sess.lep.Port
                                 select s;
                    foreach (var item in subset) {
                        item.Childs.Add(new SockUnit()
                        {
                            ID = "-",
                            Name = "accept",
                            Type = sess.type,
                            EP = sess.rep,
                            State = SockState.Opened,
                        });
                        break;
                    }
                }
                CurrentAcceptCount++;
                HistoryAcceptOpenCount++;
            }));
        }

        public void SockDel(SockSess sess)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // update SockTable
                if (sess.type == SockType.accept) {
                    foreach (var item in SockTable) {
                        if (item.Childs.Count == 0) continue;

                        foreach (var child in item.Childs) {
                            if (child.EP.Equals(sess.rep)) {
                                item.Childs.Remove(child);
                                return;
                            }
                        }
                    }
                } else if (sess.type == SockType.connect) {
                    foreach (var item in SockTable) {
                        if (item.EP.Equals(sess.rep)) {
                            item.State = SockState.Closed;
                            break;
                        }
                    }
                }
                CurrentAcceptCount--;
                HistoryAcceptCloseCount++;
            }));
        }

        public void SockOpen(IPEndPoint ep)
        {
            foreach (var item in SockTable) {
                if (item.EP.Equals(ep))
                    item.State = SockState.Opened;
            }
        }

        public void SockClose(IPEndPoint ep)
        {
            foreach (var item in SockTable) {
                if (item.EP.Equals(ep))
                    item.State = SockState.Closed;
            }
        }

        public void Logger(string log)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Log = log;
            }));
        }
    }
}

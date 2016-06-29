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

        // center port
        private int port;
        public int Port
        {
            get { return port; }
            set
            {
                port = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Port"));
            }
        }

        // socket information
        public ObservableCollection<SockUnit> SockUnitGroup { get; set; }

        // socket parse log
        public System.Windows.Controls.TextBox MsgBox { get; set; }

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
            SockUnitGroup = new ObservableCollection<SockUnit>();
            currentAcceptCount = 0;
            historyAcceptOpenCount = 0;
            historyAcceptCloseCount = 0;
        }

        public void AddSockUnit(SockUnit unit)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                switch (unit.Type) {
                    case SockType.listen:
                    case SockType.connect:
                        SockUnitGroup.Add(unit);
                        break;

                    case SockType.accept:
                        var subset = from s in SockUnitGroup
                                     where s.Type == SockType.listen && s.Lep.Port == unit.Lep.Port
                                     select s;
                        if (subset.Count() != 0) {
                            subset.First().Childs.Add(unit);
                            CurrentAcceptCount++;
                            HistoryAcceptOpenCount++;
                        }
                        break;
                }
            }));
        }

        public void DelSockUnit(SockUnit unit)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                switch (unit.Type) {
                    case SockType.listen:
                    case SockType.connect:
                        SockUnitGroup.Remove(unit);
                        break;

                    case SockType.accept:
                        var subset = from s in SockUnitGroup
                                     where s.Type == SockType.listen && s.Lep.Port == unit.Lep.Port
                                     select s;
                        if (subset.Count() != 0) {
                            subset.First().Childs.Remove(unit);
                            CurrentAcceptCount--;
                            HistoryAcceptCloseCount++;
                        }
                        break;
                }
            }));
        }

        public SockUnit FindSockUnit(SockType type, IPEndPoint lep, IPEndPoint rep)
        {
             IEnumerable<SockUnit> subset = null;
            if (type == SockType.listen)
                subset = from s in SockUnitGroup where s.Type == type && s.Lep.Equals(lep) select s;
            else if (type == SockType.connect)
                subset = from s in SockUnitGroup where s.Type == type && s.Lep != null && s.Lep.Equals(lep) select s;
            else if (type == SockType.accept) {
                foreach (var item in SockUnitGroup) {
                    subset = from s in item.Childs where s.Lep.Equals(lep) select s;
                    if (subset.Count() != 0)
                        break;
                }
            }
            else
                return null;

            if (subset != null && subset.Count() != 0)
                return subset.First();
            else
                return null;
        }

        public void SockAdd(SockType type, IPEndPoint lep, IPEndPoint rep)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (type == SockType.accept) {
                    var subset = from s in SockUnitGroup
                                 where s.Type == SockType.listen && s.Lep.Port == lep.Port
                                 select s;
                    foreach (var item in subset) {
                        item.Childs.Add(new SockUnit()
                        {
                            ID = "at" + rep.ToString(),
                            Name = "accept",
                            Type = type,
                            Lep = lep,
                            Rep = rep,
                            State = SockState.Opened,
                        });
                        break;
                    }
                }
                CurrentAcceptCount++;
                HistoryAcceptOpenCount++;
            }));
        }

        public void SockDel(SockType type, IPEndPoint lep, IPEndPoint rep)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (type == SockType.accept) {
                    foreach (var item in SockUnitGroup) {
                        if (item.Childs.Count == 0) continue;

                        foreach (var child in item.Childs) {
                            if (child.Rep.Equals(rep)) {
                                item.Childs.Remove(child);
                                return;
                            }
                        }
                    }
                } else if (type == SockType.connect) {
                    foreach (var item in SockUnitGroup) {
                        if (item.Lep != null && item.Lep.Equals(lep)) {
                            item.State = SockState.Closed;
                            break;
                        }
                    }
                }
                CurrentAcceptCount--;
                HistoryAcceptCloseCount++;
            }));
        }

        public void SockOpen(string id, IPEndPoint lep, IPEndPoint rep)
        {
            foreach (var item in SockUnitGroup) {
                if (item.ID.Equals(id)) {
                    item.Lep = lep;
                    item.Rep = rep;
                    item.State = SockState.Opened;
                    break;
                }
            }
        }

        public void SockClose(string id)
        {
            foreach (var item in SockUnitGroup) {
                if (item.ID.Equals(id)) {
                    item.State = SockState.Closed;
                    break;
                }
            }
        }

        public void Logger(string log)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MsgBox.Text.Length >= 20 * 1024)
                    MsgBox.Clear();

                MsgBox.AppendText(log);
                MsgBox.ScrollToEnd();
            }));
        }
    }
}

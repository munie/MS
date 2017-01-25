using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;
using mnn.net;

namespace SockMaster.Backend {
    class UIData : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        
        // cmd table
        public ObservableCollection<CmdUnit> CmdTable { get; set; }

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

        public UIData()
        {
            CmdTable = new ObservableCollection<CmdUnit>();
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

        public void DelSockUnit(SockType type, IPEndPoint lep, IPEndPoint rep)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                SockUnit unit = FindSockUnit(type, lep, rep);
                if (unit == null) return;

                switch (type) {
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

        public void OpenSockUnit(SockType type, IPEndPoint lep, IPEndPoint rep, string sessid)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                SockUnit unit = FindSockUnit(type, lep, rep);
                if (unit == null) return;
                unit.Lep = lep;
                unit.Rep = rep;
                unit.SESSID = sessid;
                unit.State = SockState.Opened;
            }));
        }

        public void CloseSockUnit(string id)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                SockUnit unit = FindSockUnit(id);
                if (unit == null) return;
                unit.State = SockState.Closed;
            }));
        }

        public void CloseSockUnit(SockType type, IPEndPoint lep, IPEndPoint rep)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                SockUnit unit = FindSockUnit(type, lep, rep);
                if (unit != null)
                    unit.State = SockState.Closed;
            }));
        }

        private SockUnit FindSockUnit(string id)
        {
            foreach (var item in SockUnitGroup) {
                if (item.ID.Equals(id))
                    return item;

                foreach (var child in item.Childs) {
                    if (child.ID.Equals(id))
                        return item;
                }
            }
            return null;
        }

        private SockUnit FindSockUnit(SockType type, IPEndPoint lep, IPEndPoint rep)
        {
             IEnumerable<SockUnit> subset = null;
            if (type == SockType.listen)
                subset = from s in SockUnitGroup where s.Type == type && s.Lep.Equals(lep) select s;
            else if (type == SockType.connect)
                subset = from s in SockUnitGroup where s.Type == type && s.Rep.Equals(rep) select s;
            else if (type == SockType.accept) {
                foreach (var item in SockUnitGroup) {
                    subset = from s in item.Childs where s.Rep.Equals(rep) select s;
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

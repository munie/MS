using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace SockMaster {
    class DataUI : INotifyPropertyChanged {
        public DataUI()
        {
            SockTable = new ObservableCollection<SockUnit>();
            log = new StringBuilder();
            currentAcceptCount = 0;
            historyAcceptOpenCount = 0;
            historyAcceptCloseCount = 0;
        }

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
    }
}

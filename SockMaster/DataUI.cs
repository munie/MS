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
                if (log.Length >= 20 * 1024)
                    log.Clear();
                log.Append(value);
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Log"));
            }
        }
    }
}

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
using EnvClient.Unit;

namespace EnvClient.Backend
{
    class UIData : INotifyPropertyChanged
    {
        public ObservableCollection<ListenUnit> ListenTable { get; set; }
        public ObservableCollection<AcceptUnit> AcceptTable { get; set; }
        public ObservableCollection<ModuleUnit> ModuleTable { get; set; }
        private ReaderWriterLock moduleTableLock { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

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

        public UIData()
        {
            ListenTable = new ObservableCollection<ListenUnit>();
            AcceptTable = new ObservableCollection<AcceptUnit>();
            ModuleTable = new ObservableCollection<ModuleUnit>();
            moduleTableLock = new ReaderWriterLock();

            currentAcceptCount = 0;
            historyAcceptOpenCount = 0;
            historyAcceptCloseCount = 0;
            currentPackCount = 0;
            historyPackFetchedCount = 0;
            historyPackParsedCount = 0;
        }
    }
}

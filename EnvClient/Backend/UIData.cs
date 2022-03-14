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

        private int acceptOpenCount;
        public int AcceptOpenCount
        {
            get { return acceptOpenCount; }
            set
            {
                acceptOpenCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("AcceptOpenCount"));
            }
        }

        private int acceptCloseCount;
        public int AcceptCloseCount
        {
            get { return acceptCloseCount; }
            set
            {
                acceptCloseCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("AcceptCloseCount"));
            }
        }

        private int acceptTotalCount;
        public int AcceptTotalCount
        {
            get { return acceptTotalCount; }
            set
            {
                acceptTotalCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("AcceptTotalCount"));
            }
        }

        private int packFetchedCount;
        public int PackFetchedCount
        {
            get { return packFetchedCount; }
            set
            {
                packFetchedCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("PackFetchedCount"));
            }
        }

        private int packParsedCount;
        public int PackParsedCount
        {
            get { return packParsedCount; }
            set
            {
                packParsedCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("PackParsedCount"));
            }
        }

        private int packTotalCount;
        public int PackTotalCount
        {
            get { return packTotalCount; }
            set
            {
                packTotalCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("PackTotalCount"));
            }
        }

        public UIData()
        {
            ListenTable = new ObservableCollection<ListenUnit>();
            AcceptTable = new ObservableCollection<AcceptUnit>();
            ModuleTable = new ObservableCollection<ModuleUnit>();
            moduleTableLock = new ReaderWriterLock();

            acceptOpenCount = 0;
            acceptTotalCount = 0;
            acceptCloseCount = 0;
            packFetchedCount = 0;
            packTotalCount = 0;
            packParsedCount = 0;
        }
    }
}

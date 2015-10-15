using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;

namespace SockMgr
{
    public class SockUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static readonly string StateClosed = "closed";
        public static readonly string StateListened = "listened";
        public static readonly string StateConnected = "connected";
        public static readonly string TypeListen = "listen";
        public static readonly string TypeAccept = "accept";
        public static readonly string TypeConnect = "connect";

        public string ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        private IPEndPoint ep;
        private string comment;
        public ObservableCollection<SockUnit> Childs { get; set; }

        public SockUnit()
        {
            Childs = new ObservableCollection<SockUnit>();
        }

        public IPEndPoint EP
        {
            get { return ep; }
            set
            {
                ep = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("EP"));
                }
            }
        }

        public string Comment
        {
            get { return comment; }
            set
            {
                comment = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Comment"));
                }
            }
        }
    }
}

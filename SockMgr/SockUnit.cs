using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;

namespace SockMgr
{
    public enum SockUnitState
	{
	    none = 0,
        open = 1,
        close = 2,
	}

    public class SockUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static readonly string StateClosed = "closed";
        public static readonly string StateListened = "listened";
        public static readonly string StateConnected = "connected";
        public static readonly string TypeListen = "listen";
        public static readonly string TypeAccept = "accept";
        public static readonly string TypeConnect = "connect";

        private string id;
        private string name;
        private string type;
        private IPEndPoint ep;
        private string title;
        private bool autorun;
        public ObservableCollection<SockUnit> Childs { get; set; }
        public byte[] SendBuff { get; set; }
        public int SendBuffSize { get; set; }
        public SockUnitState State { get; set; }

        public SockUnit()
        {
            Childs = new ObservableCollection<SockUnit>();
            SendBuff = null;
            SendBuffSize = 0;
            State = SockUnitState.none;
        }

        public string ID
        {
            get { return id; }
            set
            {
                id = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("ID"));
                }
            }
        }
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Name"));
                }
            }
        }
        public string Type
        {
            get { return type; }
            set
            {
                type = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Type"));
                }
            }
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
        public string Title
        {
            get { return title; }
            set
            {
                title = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Title"));
                }
            }
        }
        public bool Autorun
        {
            get { return autorun; }
            set
            {
                autorun = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Autorun"));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using mnn.net;

namespace SockMaster.Backend {
    public enum SockState {
        //None = 0,
        Opening = 1,
        Closing = 2,
        Opened = 3,
        Closed = 4,
    }

    public class SockUnit : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private string name;
        private SockType type;
        private IPEndPoint lep;
        private IPEndPoint rep;
        private SockState state;
        private string title;
        private bool autorun;
        public ObservableCollection<SockUnit> Childs { get; set; }

        public SockUnit()
        {
            Childs = new ObservableCollection<SockUnit>();
            State = SockState.Closed;
        }

        public string ID
        {
            get { return id; }
            set
            {
                id = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ID"));
            }
        }
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Name"));
            }
        }
        public SockType Type
        {
            get { return type; }
            set
            {
                type = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Type"));
            }
        }
        public IPEndPoint Lep
        {
            get { return lep; }
            set
            {
                lep = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Lep"));
            }
        }
        public IPEndPoint Rep
        {
            get { return rep; }
            set
            {
                rep = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Rep"));
            }
        }
        public SockState State
        {
            get { return state; }
            set
            {
                state = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("State"));
                UpdateTitle();
            }
        }
        public string Title
        {
            get { return title; }
            set
            {
                title = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Title"));
            }
        }
        public bool Autorun
        {
            get { return autorun; }
            set
            {
                autorun = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Autorun"));
            }
        }

        public void UpdateTitle()
        {
            if (ID == null)
                return;

            StringBuilder sb = new StringBuilder(ID);
            for (int i = 0; i < 4 - ID.Length; ++i)
                sb.Append(" ");

            if (Type == SockType.accept)
                Title = "- " + "A " + Rep.ToString() + " " + State;
            else if (Type == SockType.listen && State == SockState.Opening)
                Title = sb.ToString() + "L " + Lep.ToString() + "    " + "Listening";
            else if (Type == SockType.listen && State == SockState.Opened)
                Title = sb.ToString() + "L " + Lep.ToString() + "    " + "Listened";
            else if (Type == SockType.connect && State == SockState.Opening)
                Title = sb.ToString() + "C " + Rep.ToString() + "    " + "Connecting";
            else if (Type == SockType.connect && State == SockState.Opened)
                Title = sb.ToString() + "C " + Rep.ToString() + "    " + "Connected";
            else if (Type == SockType.listen)
                Title = sb.ToString() + "L " + Lep.ToString() + "    " + State;
            else if (Type == SockType.connect)
                Title = sb.ToString() + "C " + Rep.ToString() + "    " + State;
        }
    }
}

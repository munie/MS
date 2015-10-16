using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using Mnn.MnnSocket;

namespace SockMgr
{
    public enum SockState
	{
        //None = 0,
        Opening = 1,
        Closing = 2,
        Opened = 3,
        Closed = 4,
	}

    public class SockUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private string name;
        private SockType type;
        private Socket sock;
        private IPEndPoint ep;
        private SockState state;
        private string title;
        private bool autorun;
        public ObservableCollection<SockUnit> Childs { get; set; }
        public byte[] SendBuff { get; set; }
        public int SendBuffSize { get; set; }

        public SockUnit()
        {
            Childs = new ObservableCollection<SockUnit>();
            SendBuff = null;
            SendBuffSize = 0;
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
        public Socket Sock
        {
            get { return sock; }
            set
            {
                sock = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Sock"));
            }
        }
        public IPEndPoint EP
        {
            get { return ep; }
            set
            {
                ep = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("EP"));
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
            if (ID == null || EP == null)
                return;

            StringBuilder sb = new StringBuilder(ID);
            for (int i = 0; i < 4 - ID.Length; ++i)
                sb.Append(" ");

            if (Type == SockType.accept)
                Title = ID.ToString() + " A " + EP.ToString() + " " + State;
            else if (Type == SockType.listen && State == SockState.Opening)
                Title = sb.ToString() + "L " + EP.ToString() + "    " + "Listening";
            else if (Type == SockType.listen && State == SockState.Opened)
                Title = sb.ToString() + "L " + EP.ToString() + "    " + "Listened";
            else if (Type == SockType.connect && State == SockState.Opening)
                Title = sb.ToString() + "C " + EP.ToString() + "    " + "Connecting";
            else if (Type == SockType.connect && State == SockState.Opened)
                Title = sb.ToString() + "C " + EP.ToString() + "    " + "Connected";
            else if (Type == SockType.listen)
                Title = sb.ToString() + "L " + EP.ToString() + "    " + State;
            else if (Type == SockType.connect)
                Title = sb.ToString() + "C " + EP.ToString() + "    " + State;
        }
    }
}

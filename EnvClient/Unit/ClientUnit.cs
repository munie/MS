using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.ComponentModel;

namespace EnvClient.Unit
{
    public class ClientUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private string name;
        private IPEndPoint remoteEP;
        private string serverName;
        private int serverPort;
        private DateTime tickTime;
        private DateTime connectTime;

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
        public IPEndPoint RemoteEP
        {
            get { return remoteEP; }
            set {
                remoteEP = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("RemoteIP"));
            }
        }
        public string ServerName
        {
            get { return serverName; }
            set
            {
                serverName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ServerName"));
            }
        }
        public int ServerPort
        {
            get { return serverPort; }
            set
            {
                serverPort = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ServerPort"));
            }
        }
        public DateTime TickTime
        {
            get { return tickTime; }
            set
            {
                tickTime = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("TickTime"));
            }
        }
        public DateTime ConnectTime
        {
            get { return connectTime; }
            set {
                connectTime = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ConnectTime"));
            }
        }
    }
}

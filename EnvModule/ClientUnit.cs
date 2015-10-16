using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.ComponentModel;

namespace EnvModule
{
    public class ClientUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private string name;
        private IPEndPoint remoteEP;
        private string serverID;
        private string serverName;
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
            set
            {
                remoteEP = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("RemoteIP"));
            }
        }
        public string ServerID
        {
            get { return serverID; }
            set
            {
                serverID = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ServerID"));
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
        public DateTime ConnectTime
        {
            get { return connectTime; }
            set
            {
                connectTime = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ConnectTime"));
            }
        }
    }
}

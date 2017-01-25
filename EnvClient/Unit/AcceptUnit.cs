using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.ComponentModel;

namespace EnvClient.Unit
{
    public class AcceptUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private IPEndPoint localEP;
        private IPEndPoint remoteEP;
        private DateTime tickTime;
        private DateTime connTime;
        private string ccid;
        private string name;
        private bool admin;
        private string serverName;

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
        public IPEndPoint LocalEP
        {
            get { return localEP; }
            set
            {
                localEP = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("LocalIP"));
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
        public DateTime ConnTime
        {
            get { return connTime; }
            set {
                connTime = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ConnectTime"));
            }
        }
        public string CCID
        {
            get { return ccid; }
            set
            {
                ccid = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("CCID"));
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
        public bool Admin
        {
            get { return admin; }
            set
            {
                admin = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Admin"));
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
    }
}

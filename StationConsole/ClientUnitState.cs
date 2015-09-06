using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.ComponentModel;
using StationConsole.CtrlLayer;

namespace StationConsole
{
    public class ClientUnitState : ClientUnit, INotifyPropertyChanged
    {
        public ClientUnitState() { }
        public ClientUnitState(ClientUnit client)
        {
            ID = client.ID;
            Name = client.Name;

            RemoteEP = client.RemoteEP;
            ServerID = client.ServerID;
            ServerName = client.ServerName;
            ConnectTime = client.ConnectTime;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private string name;
        private IPEndPoint remoteEP;
        private string serverID;
        private string serverName;
        private DateTime connectTime;

        public override string ID
        {
            get { return id; }
            set
            {
                id = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ID"));
            }
        }
        public override string Name
        {
            get { return name; }
            set
            {
                name = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Name"));
            }
        }
        public override IPEndPoint RemoteEP
        {
            get { return remoteEP; }
            set {
                remoteEP = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("RemoteIP"));
            }
        }
        public override string ServerID
        {
            get { return serverID; }
            set
            {
                serverID = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ServerID"));
            }
        }
        public override string ServerName
        {
            get { return serverName; }
            set
            {
                serverName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ServerName"));
            }
        }
        public override DateTime ConnectTime
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

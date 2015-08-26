using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace StationConsole
{
    public class ClientPointState : ClientPoint, INotifyPropertyChanged
    {
        public ClientPointState() { }
        public ClientPointState(ClientPoint client)
        {
            remoteIP = client.RemoteIP;
            localPort = client.LocalPort;
            localName = "";
            connectTime = client.ConnectTime;
            ccid = client.CCID;
            name = client.Name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private string remoteIP;
        private int localPort;
        private string localName;
        private DateTime connectTime;
        private string ccid;
        private string name;

        public override string RemoteIP
        {
            get { return remoteIP; }
            set {
                remoteIP = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("RemoteIP"));
            }
        }
        public override int LocalPort
        {
            get { return localPort; }
            set
            {
                localPort = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("LocalPort"));
            }
        }
        public string LocalName
        {
            get { return localName; }
            set
            {
                localName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("LocalName"));
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
        public override string CCID
        {
            get { return ccid; }
            set {
                ccid = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("CCID"));
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
    }
}

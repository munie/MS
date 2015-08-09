using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace StationConsole
{
    public class ClientPoint : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string ipAddress;
        private int acceptedPort;
        private string acceptedName;
        private DateTime connectTime;
        private string ccid;

        public string IpAddress
        {
            get { return ipAddress; }
            set {
                ipAddress = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("IpAddress"));
            }
        }
        public int AcceptedPort
        {
            get { return acceptedPort; }
            set
            {
                acceptedPort = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("AcceptedPort"));
            }
        }
        public string AcceptedName
        {
            get { return acceptedName; }
            set
            {
                acceptedName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("AcceptedName"));
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
        public string CCID
        {
            get { return ccid; }
            set {
                ccid = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("CCID"));
            }
        }
    }
}

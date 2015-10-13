using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace SockClient
{
    class CnnUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly string StateConnected = "已连接";
        public static readonly string StateDisconned = "已断开";

        private string id;
        private string ip;
        private string port;
        private string state;
        public bool Autorun { get; set; }

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
        public string IP
        {
            get { return ip; }
            set
            {
                ip = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("IP"));
                }
            }
        }
        public string Port
        {
            get { return port; }
            set
            {
                port = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Port"));
                }
            }
        }
        public string State
        {
            get { return state; }
            set
            {
                state = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("State"));
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace EnvClient.Unit
{
    public class ServerUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int port;

        public string Name { get; set; }
        public string Protocol { get; set; }
        public string IpAddress { get; set; }
        public int Port
        {
            get { return port; }
            set
            {
                port = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Port"));
            }
        }
    }
}

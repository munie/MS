using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace EnvConsole.Unit
{
    public enum ServerTarget
    {
        center = 0,
        worker = 1,
    }

    public class ServerUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly string ListenStateStarted = "已启动";
        public static readonly string ListenStateStoped = "未启动";

        private int port;
        private string listenState;

        public string ID { get; set; }
        public string Name { get; set; }
        public ServerTarget Target { get; set; }
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
        public bool AutoRun { get; set; }
        public bool CanStop { get; set; }
        public string ListenState
        {
            get { return listenState; }
            set
            {
                listenState = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ListenState"));
            }
        }
    }
}

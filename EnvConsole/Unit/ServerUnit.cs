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
        public static readonly string TimerStateStarted = "运行中";
        public static readonly string TimerStateStoped = "未运行";
        public static readonly string TimerStateDisable = "不支持";

        private int port;
        private string listenState;
        private string timerState;
        private double timerInterval;       // 单位为秒，启动定时器时需乘1000
        private string timerCommand;

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
        public string TimerState
        {
            get { return timerState; }
            set
            {
                timerState = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("TimerState"));
            }
        }
        public double TimerInterval
        {
            get { return timerInterval; }
            set
            {
                timerInterval = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("TimerInterval"));
            }
        }
        public string TimerCommand
        {
            get { return timerCommand; }
            set
            {
                timerCommand = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("TimerCommand"));
            }
        }

        public mnn.net.deprecated.SockServer SockServer { get; set; }
        public System.Timers.Timer Timer { get; set; }
    }
}

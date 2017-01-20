using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace EnvClient.Unit
{
    public enum ServerTarget
    {
        center = 0,
        worker = 1,
    }

    public class ServerUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly string TimerStateStarted = "运行中";
        public static readonly string TimerStateStoped = "未运行";
        public static readonly string TimerStateDisable = "不支持";

        private int port;
        private string timerState;
        private double timerInterval;       // 单位为秒，启动定时器时需乘1000
        private string timerCommand;

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
    }
}

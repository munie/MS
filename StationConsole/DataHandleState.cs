using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace StationConsole
{
    public class DataHandleState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly string ListenStateRunning = "已启动";
        public static readonly string ListenStateNotRunning = "未启动";
        public static readonly string TimerStateRunning = "运行中";
        public static readonly string TimerStateNotRunning = "未运行";

        private int listenPort;
        private string listenState;
        private string timerState;
        private string chineseName;
        private string fileName;
        private double timerInterval;
        private string timerCommand;

        public int ListenPort
        {
            get { return listenPort; }
            set
            {
                listenPort = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("ListenPort"));
                }
            }
        }
        public string ListenState
        {
            get { return listenState; }
            set {
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
        public string ChineseName
        {
            get { return chineseName; }
            set {
                chineseName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ChineseName"));
            }
        }
        public string FileName
        {
            get { return fileName; }
            set {
                fileName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("FileName"));
            }
        }
        public double TimerInterval
        {
            get { return timerInterval; }
            set
            {
                timerInterval = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("TimerInterval"));
                }
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


        public System.Timers.Timer Timer = null;
    }
}

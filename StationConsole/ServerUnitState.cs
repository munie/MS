using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Net;
using StationConsole.CtrlLayer;

namespace StationConsole
{
    public class ServerUnitState : ServerUnit, INotifyPropertyChanged
    {
        public ServerUnitState() { }
        public ServerUnitState(ServerUnit server)
        {
            ID = server.ID;
            Name = server.Name;

            Protocol = server.Protocol;
            IpAddress = server.IpAddress;
            Port = server.Port;
            PipeName = server.PipeName;
            AutoRun = server.AutoRun;
            CanStop = server.CanStop;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly string ListenStateStarted = "已启动";
        public static readonly string ListenStateStoped = "未启动";
        public static readonly string TimerStateStarted = "运行中";
        public static readonly string TimerStateStoped = "未运行";
        public static readonly string TimerStateDisable = "不支持";

        private string listenState;
        private string timerState;
        private double timerInterval;       // 单位为秒，启动定时器时需乘1000
        private string timerCommand;

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

        //public override string ID { get; set; }
        //public override string Name { get; set; }
        //public override string ServerType { get; set; }
        //public override string Protocol { get; set; }
        //public override string IpAddress { get; set; }
        private int port;
        public override int Port
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
        //public override string PipeName { get; set; }

    }
}

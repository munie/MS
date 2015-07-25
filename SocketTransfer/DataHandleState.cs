using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace SocketTransfer
{
    public class DataHandleState
    {
        public static event PropertyChangedEventHandler PropertyChanged;

        public int Port { get; set; }

        private bool isPermitListen;
        public bool IsPermitListen
        {
            get
            {
                return isPermitListen;
            }
            set
            {
                isPermitListen = value;
                if (PropertyChanged != null)
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs("IsActive"));
            }
        }

        public string ListenState { get; set; }

        public string ChineseName { get; set; }

        public string FileName { get; set; }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace SockClient
{
    class CmdUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private string cmd;

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
        public string CMD
        {
            get { return cmd; }
            set
            {
                cmd = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("CMD"));
                }
            }
        }
    }
}

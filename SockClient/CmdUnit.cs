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
        private string cnnid;
        private string cmd;
        private string comment;

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
        public string CNNID
        {
            get { return cnnid; }
            set
            {
                cnnid = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("CNNID"));
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
        public string Comment
        {
            get { return comment; }
            set
            {
                comment = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Comment"));
                }
            }
        }
    }
}

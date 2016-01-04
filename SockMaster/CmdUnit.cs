using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using mnn.net;

namespace SockMaster {
    public class CmdUnit : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private string id;
        private string name;
        private string cmd;
        private bool has_header;
        private bool encrypt;
        private SockRequestContentMode content_mode;

        public string ID
        {
            get { return id; }
            set
            {
                id = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ID"));
            }
        }
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Name"));
            }
        }
        public string Cmd
        {
            get { return cmd; }
            set
            {
                cmd = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("CMD"));
            }
        }
        public bool HasHeader
        {
            get { return has_header; }
            set
            {
                has_header = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("HasHeader"));
            }
        }
        public bool Encrypt
        {
            get { return encrypt; }
            set
            {
                encrypt = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Encrypt"));
            }
        }
        public SockRequestContentMode ContentMode
        {
            get { return content_mode; }
            set
            {
                content_mode = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ContentMode"));
            }
        }
    }
}

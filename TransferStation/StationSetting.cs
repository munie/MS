using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace TransferStation
{
    public class StationSetting
    {
        public static event PropertyChangedEventHandler PropertyChanged;

        public int Port { get; set; }

        private bool isActive;
        public bool IsActive
        {
            get
            {
                return isActive;
            }
            set
            {
                isActive = value;
                if (PropertyChanged != null)
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs("IsActive"));
            }
        }

        public string State { get; set; }

        public string NameChinese { get; set; }

        public string Name { get; set; }

    }
}

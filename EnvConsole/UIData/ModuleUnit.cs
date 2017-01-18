using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace EnvConsole.UIData
{
    public class ModuleUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string fileName;
        private string fileVersion;
        private string fileComment;
        public string FileName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("FileName"));
            }
        }
        public string FileVersion
        {
            get { return fileVersion; }
            set
            {
                fileVersion = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("FileVersion"));
            }
        }
        public string FileComment
        {
            get { return fileComment; }
            set
            {
                fileComment = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("FileComment"));
            }
        }

        public mnn.misc.module.ModuleNode Module { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace EnvModule
{
    public class ModuleUnit : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual string ID { get; set; }
        public virtual string Name { get; set; }
        public virtual UInt16 Type { get; set; }
        public virtual string FilePath { get; set; }
        private string fileName;
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

        public Mnn.MnnModule.ModuleItem Module { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace StationConsole
{
    class ModuleUnitState : CtrlLayer.ModuleUnit, INotifyPropertyChanged
    {
        public ModuleUnitState() { }
        public ModuleUnitState(CtrlLayer.ModuleUnit module)
        {
            ID = module.ID;
            Name = module.Name;
            Type = module.Type;

            FilePath = module.FilePath;
            FileName = module.FileName;
            FileComment = module.FileComment;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private string fileName;
        public override string FileName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("FileName"));
            }
        }
        private string fileComment;
        public override string FileComment
        {
            get { return fileComment; }
            set
            {
                fileComment = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("FileComment"));
            }
        }

    }
}

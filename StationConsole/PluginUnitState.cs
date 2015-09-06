using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace StationConsole
{
    class PluginUnitState : CtrlLayer.PluginUnit, INotifyPropertyChanged
    {
        public PluginUnitState() { }
        public PluginUnitState(CtrlLayer.PluginUnit plugin)
        {
            ID = plugin.ID;
            Name = plugin.Name;
            FilePath = plugin.FilePath;
            FileName = plugin.FileName;
            FileComment = plugin.FileComment;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private string fileName;
        public override string FileName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("FileName"));
                }
            }
        }
        private string fileComment;
        public override string FileComment
        {
            get { return fileComment; }
            set
            {
                fileComment = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("FileComment"));
                }
            }
        }

    }
}

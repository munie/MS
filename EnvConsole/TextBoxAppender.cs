using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace EnvConsole {
    class TextBoxAppender : log4net.Appender.AppenderSkeleton {
        public TextBox MsgBox { get; set; }

        protected override void Append(log4net.Core.LoggingEvent loggingEvent)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MsgBox != null)
                    MsgBox.AppendText(loggingEvent.RenderedMessage + Environment.NewLine);
            }));
        }
    }
}

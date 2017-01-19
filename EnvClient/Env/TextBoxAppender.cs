using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace EnvClient.Env {
    class TextBoxAppender : log4net.Appender.AppenderSkeleton {
        public TextBox MsgBox { get; set; }

        protected override void Append(log4net.Core.LoggingEvent loggingEvent)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                StringBuilder sb = new StringBuilder();
                System.IO.TextWriter writer = new System.IO.StringWriter(sb);
                Layout.Format(writer, loggingEvent);
                if (MsgBox != null) {
                    MsgBox.AppendText(sb.ToString());
                    MsgBox.ScrollToEnd();
                }
            }));
        }
    }
}

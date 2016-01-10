using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.design {
    /*public*/ class TimeOut {
        public DateTime TheTimeOut { get; /*private*/ set; }
        public Delegate Func { get; private set; }
        public object[] Args { get; private set; }
        public bool Repeat { get; private set; }
        public int Interval { get; private set; }

        public TimeOut(DateTime timeout, Delegate func, object[] args, bool repeat = false, int interval = 1)
        {
            TheTimeOut = timeout;
            Func = func;
            Args = args;
            Repeat = repeat;
            Interval = interval >= 1 ? interval : 1;
        }
    }

    public class TimeOutCtl {
        private List<TimeOut> cmd_table;
        private Thread thread;

        public TimeOutCtl()
        {
            cmd_table = new List<TimeOut>();
        }

        // Methods =============================================================================

        public void Perform()
        {
            ThreadCheck(true);

            lock (cmd_table) {
                foreach (var item in cmd_table.ToArray()) {
                    if (DateTime.Now >= item.TheTimeOut) {
                        item.Func.Method.Invoke(item.Func.Target, null);
                        if (!item.Repeat)
                            cmd_table.Remove(item);
                        else {
                            do {
                                item.TheTimeOut += new TimeSpan(item.Interval);
                            } while (item.TheTimeOut > DateTime.Now);
                        }
                    }
                }
            }
        }

        public void AddTimeOut(int span, Delegate func, params object[] args)
        {
            span = span >= 1 ? span : 1;
            if (func == null) return;

            lock (cmd_table) {
                cmd_table.Add(new TimeOut(DateTime.Now + new TimeSpan(span), func, args));
            }
        }

        public void AddTimer(int interval, Delegate func, params object[] args)
        {
            interval = interval >= 1 ? interval : 1;
            if (func == null) return;

            lock (cmd_table) {
                cmd_table.Add(new TimeOut(DateTime.Now + new TimeSpan(interval), func, args, true, interval));
            }
        }

        // Self Methods ========================================================================

        private void ThreadCheck(bool isSockThread)
        {
            if (isSockThread && thread == null)
                thread = Thread.CurrentThread;

            if (thread != null && thread != Thread.CurrentThread)
                throw new ApplicationException("Only socket thread can call this function!");
        }
    }
}

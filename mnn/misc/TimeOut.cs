using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.misc {
    public class TimeOut {
        public DateTime TheTimeOut { get; protected set; }
        public Delegate Func { get; private set; }
        public object[] Args { get; private set; }

        public TimeOut(DateTime timeout, Delegate func, object[] args)
        {
            TheTimeOut = timeout;
            Func = func;
            Args = args;
        }
    }

    public class Timer : TimeOut {
        public int Interval { get; private set; }

        public Timer(DateTime timeout, Delegate func, object[] args, int interval)
            : base(timeout, func, args)
        {
            Interval = interval >= 1 ? interval : 1;
        }

        public void UpdateTimeOut()
        {
            do {
                TheTimeOut += new TimeSpan(Interval);
            } while (TheTimeOut > DateTime.Now);
        }
    }

    public class TimeOutCtl {
        private List<TimeOut> timeout_table;
        private List<Timer> timer_table;
        private Thread thread;

        public TimeOutCtl()
        {
            timeout_table = new List<TimeOut>();
            timer_table = new List<Timer>();
        }

        // Methods =============================================================================

        public void Exec()
        {
            ThreadCheck(true);

            // timeout
            lock (timeout_table) {
                foreach (var item in timeout_table.ToArray()) {
                    if (DateTime.Now >= item.TheTimeOut) {
                        item.Func.Method.Invoke(item.Func.Target, item.Args);
                        timeout_table.Remove(item);
                    }
                }
            }

            // timer
            lock (timer_table) {
                foreach (var item in timer_table) {
                    if (DateTime.Now >= item.TheTimeOut) {
                        item.Func.Method.Invoke(item.Func.Target, item.Args);
                        item.UpdateTimeOut();
                    }
                }
            }
        }

        public void AddTimeOut(int span, Delegate func, params object[] args)
        {
            span = span >= 1 ? span : 1;
            if (func == null) return;

            lock (timeout_table) {
                timeout_table.Add(new TimeOut(DateTime.Now + new TimeSpan(span), func, args));
            }
        }

        public Timer AddTimer(int interval, Delegate func, params object[] args)
        {
            interval = interval >= 1 ? interval : 1;
            if (func == null) return null;

            lock (timer_table) {
                Timer timer = new Timer(DateTime.Now + new TimeSpan(interval), func, args, interval);
                timer_table.Add(timer);
                return timer;
            }
        }

        public void DelTimer(Timer timer)
        {
            lock (timer_table) {
                timer_table.Remove(timer);
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

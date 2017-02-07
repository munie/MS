using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.glue {
    public class Loop {
        public static Loop default_loop = new Loop();

        private List<IExecable> exectab;
        private ReaderWriterLockSlim exectab_lock;

        public Loop()
        {
            exectab = new List<IExecable>();
            exectab_lock = new ReaderWriterLockSlim();
        }

        public void Add(IExecable exec)
        {
            exectab_lock.EnterWriteLock();
            exectab.Add(exec);
            exectab_lock.ExitWriteLock();
        }

        public void Remove(IExecable exec)
        {
            exectab_lock.EnterWriteLock();
            exectab.Remove(exec);
            exectab_lock.ExitWriteLock();
        }

        public void Run()
        {
            while (true) {
                exectab_lock.EnterReadLock();
                var tmpservtab = exectab.ToList();
                exectab_lock.ExitReadLock();

                if (exectab.Count == 0)
                    break;

                foreach (var item in tmpservtab) {
                    item.DoExec();

                    // should be after do_exec as give a chance to do final
                    if (item.IsClosed())
                        Remove(item);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace mnn.util {
    public interface IExecable {
        void ExecOnce(int next);
    }

    public class ExecPoll {
        private static List<IExecable> exec_group = new List<IExecable>();

        static ExecPoll()
        {
            Thread thread = new Thread(() => { while (true) RealStart(); });
            thread.IsBackground = true;
            thread.Start();
        }

        public static void Add(IExecable exec)
        {
            lock (exec_group) {
                exec_group.Add(exec);
            }
        }

        public static void Remove(IExecable exec)
        {
            lock (exec_group) {
                foreach (var item in exec_group) {
                    if (exec.Equals(item)) {
                        exec_group.Remove(item);
                        break;
                    }
                }
            }
        }

        private static void RealStart()
        {
            if (exec_group.Count == 0) {
                Thread.Sleep(1000);
                return;
            }

            lock (exec_group) {
                foreach (var item in exec_group.ToArray()) {
                    item.ExecOnce(0);
                }
            }
        }
    }
}

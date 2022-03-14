using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.glue {
    public interface IExecable {
        bool IsClosed();
        void DoExec();
    }
}

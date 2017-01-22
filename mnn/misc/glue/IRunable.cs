using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.glue {
    public interface IRunable {
        void Run();
        void RunForever();
    }
}

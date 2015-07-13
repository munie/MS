using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataProcess
{
    public interface IDataHandle
    {
        int GetIdentify();

        string Handle(string msg, System.Net.Sockets.Socket socket);
    }
}

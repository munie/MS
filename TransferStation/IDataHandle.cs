using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TransferStation
{
    public interface IDataHandle
    {
        int GetIdentity();

        string Handle(string msg);
    }
}

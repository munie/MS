using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataProcess
{
    public interface IDataHandle
    {
        string Handle(string msg);
    }
}

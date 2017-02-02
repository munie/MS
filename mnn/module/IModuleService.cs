using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.service {
    public class IModuleServiceSymbols {
        public static readonly string GET_SERVICE_TABLE = "GetServiceTable";
    }

    public interface IModuleService {
        IDictionary<string, string> GetServiceTable();
    }
}

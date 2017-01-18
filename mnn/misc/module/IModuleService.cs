using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.misc.service {
    public class IModuleServiceSymbols {
        public static readonly string GET_SERVICE_TABLE = "GetServiceTable";
        public static readonly string GET_FILTER_TABLE = "GetFilterTable";
    }

    public interface IModuleService {
        IDictionary<string, string> GetServiceTable();
        IDictionary<string, string> GetFilterTable();
    }
}

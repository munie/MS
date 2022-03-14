using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.module {
    public class IModuleFilterSymbols {
        public static readonly string GET_FILTER_TABLE = "GetFilterTable";
    }

    public interface IModuleFilter {
        IDictionary<string, string> GetFilterTable();
    }
}

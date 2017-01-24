using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mnn.service {
    public class RegexService : Service {
        public RegexService(string id, ServiceHandlerDelegate func)
            : base(id, func)
        { }

        public override bool IsMatch(ServiceRequest request)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(id);
            if (regex.IsMatch(request.id))
                return true;
            else
                return false;
        }
    }
}

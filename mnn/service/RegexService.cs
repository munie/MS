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
            IDictionary<string, dynamic> dc = Newtonsoft.Json.JsonConvert.DeserializeObject
                <Dictionary<string, dynamic>>(Encoding.UTF8.GetString(request.raw_data));

            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(id);
            if (regex.IsMatch(dc["id"]))
                return true;
            else
                return false;
        }
    }
}

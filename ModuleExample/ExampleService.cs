using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.module;
using mnn.service;

namespace ModuleExample {
	public class ExampleService : IModule, IModuleService, IModuleFilter {
		public void Init()
		{
		}

		public void Final()
		{
		}

        public IDictionary<string, string> GetServiceTable()
        {
            Dictionary<string, string> retval = new Dictionary<string, string>();
            retval.Add("service.example.hello", "HelloService");

            return retval;
        }

		public void HelloService(ServiceRequest request, ref ServiceResponse response)
		{
            request.sessdata["ccid"] = "ccid123456";
			response.data = "hello world";
		}

        public IDictionary<string, string> GetFilterTable()
        {
            Dictionary<string, string> retval = new Dictionary<string, string>();
            retval.Add("filter.example.do", "DoFilter");

            return retval;
        }

        public void DoFilter(ServiceRequest request, ref ServiceResponse response)
        {
        }
	}
}

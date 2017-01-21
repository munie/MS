using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using mnn.module;
using mnn.service;

namespace ModuleTest {
	public class TestService : IModule, IModuleService {
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

        public IDictionary<string, string> GetFilterTable()
        {
            Dictionary<string, string> retval = new Dictionary<string, string>();
            retval.Add("filter.example.do", "DoFilter");

            return retval;
        }

		public void HelloService(ServiceRequest request, ref ServiceResponse response)
		{
			response.data = "hello world";
		}

        public void DoFilter(ServiceRequest request, ref ServiceResponse response)
        {
        }
	}
}

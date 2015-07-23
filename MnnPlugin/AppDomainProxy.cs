using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace MnnPlugin
{
    class AppDomainProxy : MarshalByRefObject
    {
        private Assembly asm = null;
        private Dictionary<string, object> instances = new Dictionary<string, object>();

        // Methods =============================================================================
        public override object InitializeLifetimeService()
        {
            //Remoting对象 无限生存期
            return null;
        }

        public void LoadAssembly(string assemblyName)
        {
            /// ** 异常：如果程序集名错误
            asm = Assembly.LoadFrom(assemblyName);
        }

        public object Invoke(string interfaceName, string methodName, params object[] args)
        {
            var subset = from s in instances where s.Key.Equals(interfaceName) select s;

            // 如果实例字典中还没有指定接口对应的实例，新建之
            if (subset.Count() == 0) {
                var types = from s in asm.GetTypes()
                            where s.GetInterface(interfaceName) != null
                            select s;

                /// ** 异常：如果接口名错误
                if (types.Count() == 0)
                    throw new ApplicationException("Specified Interface is't in this Asembly.");

                object obj = asm.CreateInstance(types.First().FullName);
                instances.Add(interfaceName, obj);
            }

            // 此时subset.First()必存在
            subset = from s in instances where s.Key.Equals(interfaceName) select s;

            /// ** 异常：如果方法名错误
            MethodInfo methodInfo = subset.First().Value.GetType().GetMethod(methodName);

            /// ** 异常：如果参数错误
            return methodInfo.Invoke(subset.First().Value, args);
        }
    }
}

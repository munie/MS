using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace mnn.misc.module
{
    class AppDomainProxy : MarshalByRefObject
    {
        private Assembly asm = null;
        private List<object> instance_table = new List<object>();

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

        public bool CheckInterface(string[] ifaceNames)
        {
            // asm中的非抽象类型必须所有检验的接口
            foreach (var item in ifaceNames) {
                var types = from s in asm.GetTypes()
                            where s.GetInterface(item) != null && !s.IsAbstract
                            select s;

                if (types.Count() == 0)
                    return false;
            }

            return true;
        }

        public object Invoke(string interfaceFullName, string methodName, params object[] args)
        {
            // 1) - 查找已有实例是否提供该接口
            var instances = from s in instance_table
                            where s.GetType().GetInterface(interfaceFullName) != null
                            select s;
            
            // 2) - 查找模块是否提供该接口
            if (instances.Count() == 0) {
                var types = from s in asm.GetTypes()
                            where s.GetInterface(interfaceFullName) != null && !s.IsAbstract
                            select s;

                /// ** 异常：如果接口名错误
                if (types.Count() == 0)
                    throw new ApplicationException("Specified Interface is't in this Asembly.");

                instance_table.Add(asm.CreateInstance(types.First().FullName));
            }

            // 3) - 查找实例是否提供该方法
            MethodInfo methodInfo = instances.First().GetType().GetMethod(methodName);

            /// ** 异常：如果方法名错误
            if (methodInfo == null)
                throw new ApplicationException("Specified Method is't in this Asembly.");

            /// ** 异常：如果参数错误
            return methodInfo.Invoke(instances.First(), args);
        }
    }
}

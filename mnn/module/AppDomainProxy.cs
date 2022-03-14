using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace mnn.module {
    class AppDomainProxy : MarshalByRefObject {
        private Assembly asm = null;
        private List<object> instance_table = new List<object>();

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

        // Interface ===========================================================================

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

        public object Invoke(string interfaceFullName, string methodName, ref /*params*/ object[] args)
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

        // Method ==============================================================================

        private bool MethodCompare(System.Reflection.MethodInfo lhs, System.Reflection.MethodInfo rhs)
        {
            if (lhs.ReturnType != rhs.ReturnType)
                return false;

            var lhs_params = lhs.GetParameters();
            var rhs_params = rhs.GetParameters();

            if (lhs_params.Length != rhs_params.Length)
                return false;

            for (int i = 0; i < lhs_params.Length; i++) {
                if (lhs_params[i].ParameterType != rhs_params[i].ParameterType)
                    return false;
            }

            return true;
        }

        public bool CheckMethod(string methodName, System.Reflection.MethodInfo method)
        {
            foreach (var t in asm.GetTypes()) {
                foreach (var m in t.GetMethods()) {
                    if (m.Name.Equals(methodName) && MethodCompare(method, m))
                        return true;
                }
            }

            return false;
        }

        public string[] GetSameMethods(System.Reflection.MethodInfo method)
        {
            List<string> retval = new List<string>();

            foreach (var t in asm.GetTypes()) {
                foreach (var m in t.GetMethods()) {
                    if (MethodCompare(method, m))
                        retval.Add(m.Name);
                }
            }

            return retval.ToArray();
        }

        public object Invoke(string methodName, ref /*params*/ object[] args)
        {
            // 1) - 查找已有实例是否提供该方法
            var instances = from s in instance_table
                            where s.GetType().GetMethod(methodName) != null
                            select s;

            // 2) - 查找模块是否提供该方法
            if (instances.Count() == 0) {
                var types = from s in asm.GetTypes()
                            where s.GetMethod(methodName) != null && !s.IsAbstract
                            select s;

                /// ** 异常：如果方法名错误
                if (types.Count() == 0)
                    throw new ApplicationException("Specified Method is't in this Asembly.");

                instance_table.Add(asm.CreateInstance(types.First().FullName));
            }

            // 3) - 获得该方法
            MethodInfo methodInfo = instances.First().GetType().GetMethod(methodName);

            /// ** 异常：如果参数错误
            return methodInfo.Invoke(instances.First(), args);
        }
    }
}

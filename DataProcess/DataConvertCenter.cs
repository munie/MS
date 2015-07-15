using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using MnnSocket;

namespace DataProcess
{
    class DataHandleState
    {
        public string Name { get; set; }
        public int Port { get; set; }
        public object Instance { get; set; }
    }

    public class DataConvertCenter
    {
        // Constructor
        public DataConvertCenter(AsyncSocketListener sl)
        {
            // Initialize field sckListener
            sckListener = sl;
            sckListener.ClientReadMsg += sckListener_ClientReadMsg;

            // Load plugins
            LoadDataHandlePlugins();
        }

        // Socket
        private AsyncSocketListener sckListener = null;
        // List of IDataHandle
        private List<DataHandleState> dataHandleTable = new List<DataHandleState>();
        //private Dictionary<int, object> dataHandleTable = new Dictionary<int, object>();

        // Methods ==============================================================================
        private void LoadDataHandlePlugins()
        {
            try {
                // Get all files in directory "DataHandles"
                string appPath = System.AppDomain.CurrentDomain.BaseDirectory;
                string[] files = Directory.GetFiles(appPath + @"\DataHandles");

                // Load dll files one by one
                foreach (string file in files) {
                    LoadSpecialPlugins(file);
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void sckListener_ClientReadMsg(object sender, ClientEventArgs e)
        {
            if (e.data.StartsWith("|") == false)
                return;

            try {
                IDictionary<string, string> dc = DataUtils.AnalyzeString(e.data);

                dynamic dataHandle = null;
                object retValue;

                lock (dataHandleTable) {
                    var subset = from s in dataHandleTable where s.Port.Equals(e.localEP.Port) select s;
                    if (subset.Count() != 0) {
                        dataHandle = subset.First().Instance;
                        retValue = dataHandle.Handle(e.data, sckListener.GetSocket(e.remoteEP));
                    }
                    /*
                    if (dataHandleTable.ContainsKey(e.localEP.Port)) {
                        if (dataHandleTable.TryGetValue(e.localEP.Port, out dataHandle)) {
                            object retValue;
                            ///*
                            Type t = dataHandle.GetType();

                            MethodInfo Handle = t.GetMethod("Handle");
                            retValue = Handle.Invoke(dataHandle, new object[] { e.data, sckListener.GetSocket(e.clientEP) });
                            //if (retValue != null)
                            //    sckListener.Send(e.clientEP, (string)retValue);
                            /// /

                            retValue = dataHandle.Handle(e.data, sckListener.GetSocket(e.remoteEP));
                        }
                    }
                    */
                }

                /*
                foreach (object dataHandle in dataHandleTable) {
                    object retValue;
                    Type t = dataHandle.GetType();

                    MethodInfo GetIdentify = t.GetMethod("GetIdentify");
                    retValue = GetIdentify.Invoke(dataHandle, null);

                    if (((string)retValue).Equals("QXZ")) {
                        MethodInfo Handle = t.GetMethod("Handle");
                        retValue = Handle.Invoke(dataHandle, new object[] { e.data });
                        if (retValue != null)
                            sckListener.Send(e.clientEP, (string)retValue);
                    }
                }
                 * */
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        public void LoadSpecialPlugins(string file)
        {
            // File information
            FileInfo fileInfo = new FileInfo(file);

            // Verify file
            foreach (var item in dataHandleTable) {
                if (item.Name.Equals(fileInfo.Name))
                    throw new ApplicationException("Assembly is already loaded.");
            }

            // Load dll
            Assembly asm = Assembly.LoadFrom(file);

            // Get all types defined in this dll
            Type[] types = asm.GetTypes();

            // Instantiate every class derived "IDataHandle" from this dll
            int dataHandleKey;
            dynamic dataHandleInstance;
            foreach (Type t in types) {
                if (t.GetInterface("IDataHandle") != null) {
                    dataHandleInstance = Activator.CreateInstance(t);
                    dataHandleKey = dataHandleInstance.GetIdentify();
                    /*
                    dataHandleInstance = asm.CreateInstance(t.FullName);
                    MethodInfo GetIdentify = t.GetMethod("GetIdentify");
                    dataHandleKey = (string)GetIdentify.Invoke(dataHandleInstance, null);
                     * */
                    lock (dataHandleTable) {
                        dataHandleTable.Add(new DataHandleState()
                        {
                            Name = fileInfo.Name,
                            Port = dataHandleKey,
                            Instance = dataHandleInstance
                        });
                    }
                }
            }
        }

        public void UnloadSpecialPlugins(string file)
        {
            string callingDomainName = AppDomain.CurrentDomain.FriendlyName;//Thread.GetDomain().FriendlyName; 
            Console.WriteLine(callingDomainName);

            AppDomain ad = AppDomain.CreateDomain("DLL Unload test");
            ProxyObject obj = (ProxyObject)ad.CreateInstanceFromAndUnwrap(@"UnloadDll.exe", "UnloadDll.ProxyObject");
            obj.LoadAssembly();
            obj.Invoke("TestDll.Class1", "Test", "It's a test");
            AppDomain.Unload(ad);
            obj = null;
            Console.ReadLine(); 
        }

        public Dictionary<string, int> GetHandleStatus()
        {
            Dictionary<string, int> dc = new Dictionary<string, int>();

            foreach (var item in dataHandleTable) {
                dc.Add(item.Name, item.Port);
            }

            return dc;
        }
    }

    class ProxyObject : MarshalByRefObject
    {
        Assembly assembly = null;
        public void LoadAssembly()
        {
            assembly = Assembly.LoadFile(@"TestDLL.dll");
        }
        public bool Invoke(string fullClassName, string methodName, params Object[] args)
        {
            if (assembly == null)
                return false;
            Type tp = assembly.GetType(fullClassName);
            if (tp == null)
                return false;
            MethodInfo method = tp.GetMethod(methodName);
            if (method == null)
                return false;
            Object obj = Activator.CreateInstance(tp);
            method.Invoke(obj, args);
            return true;
        }
    }

}

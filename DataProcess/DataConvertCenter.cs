using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using MnnSocket;

namespace DataProcess
{
    public partial class DataConvertCenter
    {
        class DataHandleState
        {
            public string File { get; set; }
            public int Port { get; set; }
            public object Instance { get; set; }
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

    public partial class DataConvertCenter
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

        public EventHandler<HandleResultEventArgs> HandleResult;

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
            if (e.Data.StartsWith("|") == false)
                return;

            try {
                IDictionary<string, string> dc = DataUtils.AnalyzeString(e.Data);

                dynamic dataHandle = null;
                object retValue;

                lock (dataHandleTable) {
                    var subset = from s in dataHandleTable where s.Port.Equals(e.LocalEP.Port) select s;
                    if (subset.Count() != 0) {
                        dataHandle = subset.First().Instance;
                        retValue = dataHandle.Handle(e.Data, sckListener.GetSocket(e.RemoteEP));

                        if (HandleResult != null && retValue != null)
                            HandleResult(this, new HandleResultEventArgs(e.RemoteEP, (string)retValue, ""));
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
            if (fileInfo.Extension.ToLower().Equals(".dll") == false)
                throw new ApplicationException("Assembly isn't a dll file.");
            foreach (var item in dataHandleTable) {
                if (item.Equals(fileInfo.Name))
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
                            File = file,
                            Port = dataHandleKey,
                            Instance = dataHandleInstance,
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

        public Dictionary<int, string> GetDataHandleStatus()
        {
            Dictionary<int, string> dc = new Dictionary<int, string>();

            foreach (var item in dataHandleTable) {
                dc.Add(item.Port, item.File);
            }

            return dc;
        }
    }

}

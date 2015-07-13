using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using MnnSocket;

namespace DataProcess
{
    public class DataConvertCenter
    {
        // Constructor
        public DataConvertCenter(AsyncSocketListener sl)
        {
            // Initialize field sckListener
            sckListener = sl;
            sckListener.clientReadMsg += sckListener_clientReadMsg;

            // Load plugins
            LoadDataHandlePlugins();
        }

        // Socket
        public AsyncSocketListener sckListener = null;
        // List of IDataHandle
        private Dictionary<int, object> dataHandleTable = new Dictionary<int, object>();

        // Methods ==============================================================================
        private void LoadDataHandlePlugins()
        {
            try {
                // Get all files in directory "DataHandles"
                string appPath = System.AppDomain.CurrentDomain.BaseDirectory;
                string[] files = Directory.GetFiles(appPath + @"\DataHandles");

                // Load dll files one by one
                foreach (string file in files) {
                    if (file.ToUpper().EndsWith(".DLL")) {
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
                                dataHandleTable.Add(dataHandleKey, dataHandleInstance);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void sckListener_clientReadMsg(object sender, ClientEventArgs e)
        {
            if (e.data.StartsWith("|") == false)
                return;

            try {
                IDictionary<string, string> dc = DataUtils.AnalyzeString(e.data);

                dynamic dataHandle = null;

                if (dataHandleTable.ContainsKey(e.localEP.Port)) {
                    if (dataHandleTable.TryGetValue(e.localEP.Port, out dataHandle)) {
                        object retValue;
                        /*
                        Type t = dataHandle.GetType();

                        MethodInfo Handle = t.GetMethod("Handle");
                        retValue = Handle.Invoke(dataHandle, new object[] { e.data, sckListener.GetSocket(e.clientEP) });
                        //if (retValue != null)
                        //    sckListener.Send(e.clientEP, (string)retValue);
                         * */

                        retValue = dataHandle.Handle(e.data, sckListener.GetSocket(e.remoteEP));
                    }
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
    }
}

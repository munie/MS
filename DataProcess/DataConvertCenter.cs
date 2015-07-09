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
        // Socket
        public AsyncSocketListener sckListener = null;

        // List of IDataHandle
        private List<object> dataHandleTable = new List<object>();

        // Methods ==============================================================================
        // Constructor
        public DataConvertCenter(AsyncSocketListener sl)
        {
            // Initialize field sckListener
            sckListener = sl;
            sckListener.clientMessage += new ClientMessageEventHandler(sckListener_clientMessage);

            // Load plugins
            LoadDataHandlePlugins();
        }

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
                        foreach (Type t in types) {
                            if (t.GetInterface("IDataHandle") != null)
                                dataHandleTable.Add(asm.CreateInstance(t.FullName));
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void sckListener_clientMessage(object sender, ClientEventArgs e)
        {
            try {
                foreach (object dataHandle in dataHandleTable) {
                    Type t = dataHandle.GetType();
                    MethodInfo Handle = t.GetMethod("Handle");
                    object retValue = Handle.Invoke(dataHandle, new object[] { e.data });
                    string s = (string)retValue;
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

    }
}

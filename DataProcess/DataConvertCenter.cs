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
            sckListener.clientMessage += sckListener_clientMessage;

            // Load plugins
            LoadDataHandlePlugins();
        }

        // Socket
        public AsyncSocketListener sckListener = null;
        // List of IDataHandle
        private Dictionary<string, object> dataHandleTable = new Dictionary<string, object>();

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
                        string dataHandleKey;
                        object dataHandleInstance;
                        foreach (Type t in types) {
                            if (t.GetInterface("IDataHandle") != null) {
                                dataHandleInstance = asm.CreateInstance(t.FullName);
                                MethodInfo GetIdentify = t.GetMethod("GetIdentify");
                                dataHandleKey = (string)GetIdentify.Invoke(dataHandleInstance, null);
                                dataHandleTable.Add(dataHandleKey, dataHandleInstance);
                                //dataHandleTable.Add(asm.CreateInstance(t.FullName));
                            }
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
                IDictionary<string, string> dc = AnalyzeString(e.data);

                object dataHandle = null;

                if (dataHandleTable.ContainsKey(dc["HT"])) {
                    if (dataHandleTable.TryGetValue(dc["HT"], out dataHandle)) {
                        object retValue;
                        Type t = dataHandle.GetType();

                        MethodInfo Handle = t.GetMethod("Handle");
                        retValue = Handle.Invoke(dataHandle, new object[] { e.data });
                        if (retValue != null)
                            sckListener.Send(e.clientEP, (string)retValue);
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

        /// <summary>
        /// 字典：字符与值对应
        /// </summary>
        /// <param name="mes"></param>
        /// <returns></returns>
        public static IDictionary<string, string> AnalyzeString(string mes)
        {
            string txt = mes;
            string[] fields = txt.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            string[] kv;
            bool hasTheSameKey = false;
            foreach (string field in fields) {
                kv = field.Split("=".ToCharArray());
                //mod by zxq 2013-10-01
                if (dict.ContainsKey(kv[0])) {
                    if (!hasTheSameKey) {
                        hasTheSameKey = true;
                    }
                    dict[kv[0]] = kv[1];
                }
                else {
                    dict.Add(kv[0], kv[1]);
                }

            }
            if (hasTheSameKey) {
                //Program.writeLog_WithTime(string.Format("收到的消息中键值重复。消息详情为：{0}", mes));
            }
            return dict;
        }
    }
}

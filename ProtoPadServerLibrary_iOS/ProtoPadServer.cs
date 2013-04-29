using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using MonoTouch.UIKit;
using ServiceDiscovery;

namespace ProtoPadServerLibrary_iOS
{
    public class ProtoPadServer : IDisposable
    {
        public IPAddress LocalIPAddress { get; private set; }
        public int ListeningPort { get; private set; }
        public string BroadcastedAppName { get; private set; }

        private readonly UIApplicationDelegate _appDelegate;
        private readonly UIWindow _window;
        private readonly SimpleHttpServer _httpServer;
        private readonly UdpDiscoveryServer _udpServer;

        /// <summary>
        /// Starts listening for ProtoPad clients, and allows them to connect and access the UIApplicationDelegate and UIWindow you pass in
        /// WARNING: do not dispose until you are done listening for ProtoPad client events. Usually you will want to dispose only upon exiting the app.
        /// </summary>
        /// <param name="appDelegate">Supply your main application delegate here. This will be made scriptable from the ProtoPad Client.</param>
        /// <param name="window">Supply your main application window here. This will be made scriptable from the ProtoPad Client.</param>
        public static ProtoPadServer Create(UIApplicationDelegate appDelegate, UIWindow window, int? overrideListeningPort = null, string overrideBroadcastedAppName = null)
        {
            return new ProtoPadServer(appDelegate, window, overrideListeningPort, overrideBroadcastedAppName);
        }

        private ProtoPadServer(UIApplicationDelegate appDelegate, UIWindow window, int? overrideListeningPort = null, string overrideBroadcastedAppName = null)
        {
            _appDelegate = appDelegate;
            _window = window;

            BroadcastedAppName = overrideBroadcastedAppName ?? String.Format("ProtoPad Service on iOS Device {0}", UIDevice.CurrentDevice.Name);
            ListeningPort = overrideListeningPort ?? 8080;
            LocalIPAddress = Helpers.GetCurrentIPAddress();

            var mainMonotouchAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.ToLower() == "monotouch");

            _httpServer = new SimpleHttpServer(responseBytes =>
            {
                var response = "{}";
                var remoteCommandDoneEvent = new AutoResetEvent(false);
                _appDelegate.InvokeOnMainThread(() => Response(responseBytes, remoteCommandDoneEvent, ref response));
                remoteCommandDoneEvent.WaitOne();
                return response;
            }, ListeningPort, "iOS", mainMonotouchAssembly.FullName);

            //_udpServer = new UdpDiscoveryServer(BroadcastedAppName, String.Format("http://{0}:{1}/", LocalIPAddress, ListeningPort), Helpers.GetCurrentIPAddress());          
            _udpServer = new UdpDiscoveryServer(BroadcastedAppName, String.Format("http://{0}:{1}/", LocalIPAddress, ListeningPort));
        }

        public static string JsonEncode(object value)
        {
            var serializer = new DataContractJsonSerializer(value.GetType());
            string resultJSON;
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                resultJSON = Encoding.Default.GetString(stream.ToArray());
                stream.Close();
            }
            return resultJSON;
        }

        private void Response(byte[] responseBytes, EventWaitHandle remoteCommandDoneEvent, ref string response)
        {
            try
            {
                var executeResponse = ExecuteLoadedAssemblyString(responseBytes, _appDelegate, _window);
                var dumpValue = executeResponse.GetDumpValues();
                if (dumpValue != null)
                {
                    //executeResponse.Results = dumpValue.Select(v => new ResultPair(v.Item1, Dumper.ObjectToDumpValue(v.Item2, v.Item3, executeResponse.GetMaxEnumerableItemCount()))).ToList();
                    executeResponse.Results = dumpValue.Select(v => new ResultPair(v.Description, Dumper.ObjectToDumpValue(v.Value, v.Level, executeResponse.GetMaxEnumerableItemCount()))).ToList();
                }
                response = JsonEncode(executeResponse);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                remoteCommandDoneEvent.Set();
            }
        }

        private static ExecuteResponse ExecuteLoadedAssemblyString(byte[] loadedAssemblyBytes, UIApplicationDelegate appDelegate, UIWindow window)
        {
            MethodInfo printMethod;

            object loadedInstance;
            try
            {
                // TODO: create new AppDomain for each loaded assembly, to prevent memory leakage
                var loadedAssembly = AppDomain.CurrentDomain.Load(loadedAssemblyBytes);
                var loadedType = loadedAssembly.GetType("__MTDynamicCode");
                if (loadedType == null) return null;
                loadedInstance = Activator.CreateInstance(loadedType);
                printMethod = loadedInstance.GetType().GetMethod("Main");
            }
            catch (Exception e)
            {
                return new ExecuteResponse { ErrorMessage = e.Message };
            }

            var response = new ExecuteResponse();
            try
            {
                printMethod.Invoke(loadedInstance, new object[] { appDelegate, window });
                var dumpsRaw = loadedInstance.GetType().GetField("___dumps").GetValue(loadedInstance) as IEnumerable;
                response.SetDumpValues(dumpsRaw.Cast<object>().Select(GetDumpObjectFromObject).ToList());
                response.SetMaxEnumerableItemCount(Convert.ToInt32(loadedInstance.GetType().GetField("___maxEnumerableItemCount").GetValue(loadedInstance)));
            }
            catch (Exception e)
            {
                var lineNumber = loadedInstance.GetType().GetField("___lastExecutedStatementOffset").GetValue(loadedInstance);
                response.ErrorMessage = String.Format("___EXCEPTION_____At offset: {0}__{1}", lineNumber, e.InnerException.Message);
            }

            return response;
        }

        public static DumpHelpers.DumpObj GetDumpObjectFromObject(object value)
        {                
            var objType = value.GetType();
            return new DumpHelpers.DumpObj(Convert.ToString(objType.GetField("Description").GetValue(value)),
                    objType.GetField("Value").GetValue(value),
                    Convert.ToInt32(objType.GetField("Level").GetValue(value)),
                    Convert.ToBoolean(objType.GetField("ToDataGrid").GetValue(value)));
        }

        public void Dispose()
        {
            if (_httpServer != null) _httpServer.Dispose();
            if (_udpServer != null) _udpServer.Dispose();
        }
    }
}
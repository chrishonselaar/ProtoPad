using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Android.App;
using Android.Net.Wifi;
using Android.Views;
using ServiceDiscovery;

namespace ProtoPadServerLibrary_Android
{
    public sealed class ProtoPadServer: IDisposable
    {
        private readonly View _window;
        private readonly Activity _contextActivity;
        private readonly SimpleHttpServer _httpServer;
        private readonly UdpDiscoveryServer _udpServer;
        private readonly WifiManager.MulticastLock _mcLock;

        public IPAddress LocalIPAddress { get; private set; }
        public int ListeningPort { get; private set; }
        public string BroadcastedAppName { get; private set; }

        /// <summary>
        /// Starts listening for ProtoPad clients, and allows them to connect and access the View you pass in
        /// WARNING: do not dispose until you are done listening for ProtoPad client events. Usually you will want to dispose only upon exiting the app.
        /// </summary>
        /// <param name="window">Supply your main application view here. This will be made scriptable from the ProtoPad Client.</param>
        public static ProtoPadServer Create(View window, int? overrideListeningPort = null, string overrideBroadcastedAppName = null)
        {
            return new ProtoPadServer(window, overrideListeningPort, overrideBroadcastedAppName);
        }

        private ProtoPadServer(View window, int? overrideListeningPort = null, string overrideBroadcastedAppName = null)
        {
            _window = window;
            _contextActivity = window.Context as Activity;

            _httpServer = new SimpleHttpServer(responseBytes =>
            {
                var response = "{}";
                var remoteCommandDoneEvent = new AutoResetEvent(false);
                _contextActivity.RunOnUiThread(() => Response(responseBytes, remoteCommandDoneEvent, ref response));                
                remoteCommandDoneEvent.WaitOne();
                return response;
            });

            IPAddress broadCastAddress;
            using (var wifi = _contextActivity.GetSystemService(Android.Content.Context.WifiService) as WifiManager)
            {
                _mcLock = wifi.CreateMulticastLock("ProtoPadLock");
                _mcLock.Acquire();
                broadCastAddress = GetBroadcastAddress(wifi);                
            }

            BroadcastedAppName = overrideBroadcastedAppName ?? String.Format("ProtoPad Service on ANDROID Device {0}", Android.OS.Build.Model);
            ListeningPort = overrideListeningPort ?? 8080;
            LocalIPAddress = Helpers.GetCurrentIPAddress();
            
            _udpServer = new UdpDiscoveryServer(BroadcastedAppName, String.Format("http://{0}:{1}/", LocalIPAddress, ListeningPort), broadCastAddress);          
        }

        public void Dispose()
        {
            if (_mcLock != null)
            {
                _mcLock.Release();
                _mcLock.Dispose();     
            }
            if (_httpServer != null) _httpServer.Dispose();
            if (_udpServer != null) _udpServer.Dispose();
        }

        private void Response(byte[] responseBytes, EventWaitHandle remoteCommandDoneEvent, ref string response)
        {
            try
            {
                var executeResponse = ExecuteLoadedAssemblyString(responseBytes, _window);
                if (executeResponse.DumpValues != null)
                {
                    executeResponse.Results = executeResponse.DumpValues.Select(v => new Tuple<string, DumpValue>(v.Item1, Dumper.ObjectToDumpValue(v.Item2, v.Item3, executeResponse.MaxEnumerableItemCount))).ToList();
                }
                response = JsonEncode(executeResponse);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            finally
            {
                remoteCommandDoneEvent.Set();
            }
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

        public IPAddress GetBroadcastAddress(WifiManager wifi)
        {
            var dhcp = wifi.DhcpInfo;
            var broadcast = (dhcp.IpAddress & dhcp.Netmask) | ~dhcp.Netmask;
            var quads = new byte[4];
            for (var k = 0; k < 4; k++) quads[k] = (byte)((broadcast >> k * 8) & 0xFF);
            return new IPAddress(quads);
        }

        private class ExecuteResponse
        {
            public string ErrorMessage { get; set; }
            public List<Tuple<string, object, int, bool>> DumpValues;
            public List<Tuple<string, DumpValue>> Results { get; set; }
            public int MaxEnumerableItemCount;
        }

        private static ExecuteResponse ExecuteLoadedAssemblyString(byte[] loadedAssemblyBytes, View window)
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
                printMethod.Invoke(loadedInstance, new object[] { window });
                response.DumpValues = loadedInstance.GetType().GetField("___dumps").GetValue(loadedInstance) as List<Tuple<string, object, int, bool>>;
                response.MaxEnumerableItemCount = Convert.ToInt32(loadedInstance.GetType().GetField("___maxEnumerableItemCount").GetValue(loadedInstance));
            }
            catch (Exception e)
            {
                var lineNumber = loadedInstance.GetType().GetField("___lastExecutedStatementOffset").GetValue(loadedInstance);
                response.ErrorMessage = String.Format("___EXCEPTION_____At offset: {0}__{1}", lineNumber, e.InnerException.Message);
            }

            return response;
        }
    }
}
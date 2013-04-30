using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Android.App;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using ServiceDiscovery;
using Debug = System.Diagnostics.Debug;

namespace ProtoPadServerLibrary_Android
{
    public sealed class ProtoPadServer: IDisposable
    {
        public IPAddress LocalIPAddress { get; private set; }
        public int ListeningPort { get; private set; }
        public string BroadcastedAppName { get; private set; }

        private readonly Activity _contextActivity;
        private readonly SimpleHttpServer _httpServer;
        private readonly UdpDiscoveryServer _udpServer;
        private readonly WifiManager.MulticastLock _mcLock;

        /// <summary>
        /// Starts listening for ProtoPad clients, and allows them to connect and access the Activity you pass in
        /// Make sure to enable ACCESS_WIFI_STATE, CHANGE_WIFI_MULTICAST_STATE permissions in your app manifest, for autodiscovery to work - as well as any other permissions you yourself may need of course!
        /// WARNING: do not dispose until you are done listening for ProtoPad client events. Usually you will want to dispose only upon exiting the app.
        /// </summary>
        /// <param name="activity">Supply your main application Activity here. This will be made scriptable from the ProtoPad Client.</param>
        public static ProtoPadServer Create(Activity activity, int? overrideListeningPort = null, string overrideBroadcastedAppName = null)
        {
            return new ProtoPadServer(activity, overrideListeningPort, overrideBroadcastedAppName);
        }

        private ProtoPadServer(Activity activity, int? overrideListeningPort = null, string overrideBroadcastedAppName = null)
        {
            _contextActivity = activity;

            BroadcastedAppName = overrideBroadcastedAppName ?? String.Format("ProtoPad Service on ANDROID Device {0}", Android.OS.Build.Model);
            ListeningPort = overrideListeningPort ?? 8080;
            LocalIPAddress = Helpers.GetCurrentIPAddress();

            var mainMonodroidAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.ToLower() == "mono.android");

            var requestHandlers = new Dictionary<string, Func<byte[], string>>
                {
                    {"GetMainXamarinAssembly", data => mainMonodroidAssembly.FullName},
                    {"WhoAreYou", data => "Android"},
                    {"ExecuteAssembly", data =>
                        {
                            var response = "{}";
                            var remoteCommandDoneEvent = new AutoResetEvent(false);
                            _contextActivity.RunOnUiThread(() => Response(data, remoteCommandDoneEvent, ref response));                
                            remoteCommandDoneEvent.WaitOne();
                            return response;
                        }
                    }
                };

            _httpServer = new SimpleHttpServer(ListeningPort, requestHandlers);

            IPAddress broadCastAddress;
            using (var wifi = _contextActivity.GetSystemService(Android.Content.Context.WifiService) as WifiManager)
            {
                try
                {
                    _mcLock = wifi.CreateMulticastLock("ProtoPadLock");
                    _mcLock.Acquire();

                }
                catch (Java.Lang.SecurityException e)
                {
                    Debug.WriteLine("Could not optain Multicast lock: {0}. Did you enable CHANGE_WIFI_MULTICAST_STATE permission in your app manifest?", e.Message);
                }

                broadCastAddress = GetBroadcastAddress(wifi);                
            }

            var inEmulator = Build.Brand.Equals("generic", StringComparison.InvariantCultureIgnoreCase);
            _udpServer = new UdpDiscoveryServer(BroadcastedAppName, String.Format("http://{0}:{1}/", inEmulator ? "localhost" : LocalIPAddress.ToString(), inEmulator ? "?" : ListeningPort.ToString()), broadCastAddress);          
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
                var executeResponse = ExecuteLoadedAssemblyString(responseBytes, _contextActivity);
                var dumpValues = executeResponse.GetDumpValues();
                if (dumpValues != null)
                {
                    executeResponse.Results = dumpValues.Select(v => new ResultPair(v.Description, Dumper.ObjectToDumpValue(v.Value, v.Level, executeResponse.GetMaxEnumerableItemCount()))).ToList();
                }
				var serializeResponse = new ExecuteResponseSerialize();
				serializeResponse.Results = executeResponse.Results;
				serializeResponse.ErrorMessage  =executeResponse.ErrorMessage;
				//executeResponse.SetDumpValues (null);
				response = JsonEncode(serializeResponse);
            }
            catch (Exception e)
            {
                //Debug.WriteLine(e.Message);
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
            DhcpInfo dhcp;
            try
            {
                dhcp = wifi.DhcpInfo;
            }
            catch (Java.Lang.SecurityException e)
            {
                Debug.WriteLine("Could not obtain Wifi information: {0}. Did you enable ACCESS_WIFI_STATE permission in your app manifest?", e.Message);
                return null;
            }
            var broadcast = (dhcp.IpAddress & dhcp.Netmask) | ~dhcp.Netmask;
            var quads = new byte[4];
            for (var k = 0; k < 4; k++) quads[k] = (byte)((broadcast >> k * 8) & 0xFF);
            return new IPAddress(quads);
        }

        private static ExecuteResponse ExecuteLoadedAssemblyString(byte[] loadedAssemblyBytes, Activity activity)
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
                printMethod.Invoke(loadedInstance, new object[] { activity, activity.Window });
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
            var dumpObject = new DumpHelpers.DumpObj(Convert.ToString(objType.GetField("Description").GetValue(value)),
                objType.GetField("Value").GetValue(value),
                Convert.ToInt32(objType.GetField("Level").GetValue(value)),
                Convert.ToBoolean(objType.GetField("ToDataGrid").GetValue(value))
            );
            return dumpObject;
        }
    }
}
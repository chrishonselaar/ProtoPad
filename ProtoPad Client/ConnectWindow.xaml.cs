using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ServiceDiscovery;

namespace ProtoPad_Client
{
    public partial class ConnectWindow
    {
        public MainWindow.DeviceItem SelectedDeviceItem = null;
        private readonly UdpDiscoveryClient _udpDiscoveryClient;
        public const int MulticastForwardedHostPort = 5356; // todo: auto-find available port
        public const int HttpForwardedHostPort = 18080; // todo: auto-find available port
        private readonly System.Windows.Threading.DispatcherTimer _dispatcherTimer;
        private int _ticksPassed;

        public ConnectWindow()
        {
            InitializeComponent();

            _udpDiscoveryClient = new UdpDiscoveryClient(
                ready => { },
                (name, address) => Dispatcher.Invoke((Action)(() =>
                {
                    if (address.Contains(":?/")) // Android emulator, so use forwarded ports
                    {
                        // todo: use telnet to discover already set up port forwards, instead of hardcoding
                        address = address.Replace(":?/", String.Format(":{0}/", HttpForwardedHostPort));
                    }
                    var deviceItem = new MainWindow.DeviceItem
                    {
                        DeviceAddress = address,
                        DeviceName = name,
                        DeviceType =
                            name.StartsWith("ProtoPad Service on ANDROID Device ")
                                ? MainWindow.DeviceTypes.Android
                                : MainWindow.DeviceTypes.iOS
                    };
                    if (!DevicesList.Items.Cast<object>().Any(i => (i as MainWindow.DeviceItem).DeviceAddress == deviceItem.DeviceAddress))
                    {
                        DevicesList.Items.Add(deviceItem);
                    }
                    DevicesList.IsEnabled = true;
                    //LogToResultsWindow("Found '{0}' on {1}", name, address);
                })));

            _dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            _dispatcherTimer.Tick += (s, a) =>
            {
                if (_ticksPassed > 2)
                {
                    _dispatcherTimer.Stop();
                    if (DevicesList.Items.Count == 1) DevicesList.SelectedIndex = 0;
                }
                _udpDiscoveryClient.SendServerPing();
                _ticksPassed++;
            };
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(200);

            FindApps();
        }

        private void FindAppsButton_Click(object sender, RoutedEventArgs e)
        {
            FindApps();
        }

        private void FindApps()
        {
            DevicesList.Items.Clear();
            SearchForRunningProtoPadServers();
            ConnectButton.IsEnabled = false;
        }

        private void SearchForRunningProtoPadServers()
        {
            _udpDiscoveryClient.SendServerPing();
            _ticksPassed = 0;
            _dispatcherTimer.Start();
        }

        private void AddManualIPButton_Click(object sender, RoutedEventArgs e)
        {
            var promptWindow = new PromptIPAddressWindow();
            if (!promptWindow.ShowDialog().Value) return;
            var address = String.Format("http://{0}:{1}", promptWindow.IPAddressTextBox.Text, promptWindow.PortTextBox.Text);
            var deviceType = QuickConnect(address);
            if (!deviceType.HasValue)
            {
                MessageBox.Show(String.Format("Could not connect to {0}", address));
                return;
            }
            var deviceItem = new MainWindow.DeviceItem
            {
                DeviceAddress = address,
                DeviceName = String.Format("{0} app on {1}", deviceType, address),
                DeviceType = deviceType.Value
            };
            if (!DevicesList.Items.Cast<object>().Any(i => (i as MainWindow.DeviceItem).DeviceAddress == deviceItem.DeviceAddress))
            {
                DevicesList.Items.Add(deviceItem);
            }
            DevicesList.IsEnabled = true;
        }

        private static List<int> FindEmulatorPortCandidates()
        {
            var tcpRows = ManagedIpHelper.GetExtendedTcpTable(true);
            return (from tcpRow in tcpRows let process = Process.GetProcessById(tcpRow.ProcessId) where process != null where process.ProcessName.Contains("emulator-arm") select tcpRow.LocalEndPoint.Port).Distinct().ToList();
        }

        private void ConfigureAndroidEmulatorButton_Click(object sender, RoutedEventArgs e)
        {
            var emulatorPortCandidates = FindEmulatorPortCandidates();
            if (!emulatorPortCandidates.Any())
            {
                MessageBox.Show("No emulators found - are you sure the emulator is online?");
                return;
            }
            var results = emulatorPortCandidates.ToDictionary(c => c, SetupPortForwardingOnAndroidEmulator);
            var successResults = results.Where(r => r.Value.HasValue && r.Value.Value).Select(r => r.Key).ToList();
            if (successResults.Any())
            {
                MessageBox.Show(String.Format("Emulator found at port {0}, and configured successfully! Hit 'Find servers' to auto-discover your running app on this/these emulator(s).", String.Join(", ", successResults)));
                return;
            }
            var halfResults = results.Where(r => r.Value.HasValue && !r.Value.Value).Select(r => r.Key).ToList();
            if (halfResults.Any())
            {
                MessageBox.Show(String.Format("Emulator found at port {0}, but it may have already been configured, or was not able to be configured successfully. Please retry auto-discovery.", String.Join(", ", halfResults)));
                return;
            }
            var emptyResults = results.Where(r => !r.Value.HasValue).Select(r => r.Key);
            MessageBox.Show(String.Format("Emulator found at port {0}, but it could not be telnet-connected to. Please retry auto-discovery.", String.Join(", ", emptyResults)));
        }

        /// <summary>
        /// Tries to find and Telnet-connect to the (first) running Android Emulator (AVD)
        /// And tries to set up port forwarding on it, so that the ProtoPad Http (command) and Udp (discovery) servers are accessible from your host machine.
        /// </summary>
        /// <returns>Whether the port forwarding setup was succesful (might fail if already set up or ports busy)</returns>
        private static bool? SetupPortForwardingOnAndroidEmulator(int port = 5554)
        {
            var udpCommandResponse = "";
            var tcpCommandResponse = "";
            try
            {
                using (var tc = new TelnetConnection("localhost", port))
                {
                    var connectResponse = tc.Read();
                    if (!connectResponse.Contains("Android Console")) return null;

                    tc.WriteLine(String.Format("redir add udp:{0}:{1}", MulticastForwardedHostPort, UdpDiscoveryServer.UdpServerPort));
                    udpCommandResponse = tc.Read();
                    tc.WriteLine(String.Format("redir add tcp:{0}:{1}", HttpForwardedHostPort, 8080));
                    tcpCommandResponse = tc.Read();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unexpected error during Android Emulator Telnet session: {0}", e.Message);
                return null;
            }

            // response in case already set up:
            // "KO: host port already active, use 'redir del' to remove first"

            return udpCommandResponse.Contains("OK") && tcpCommandResponse.Contains("OK");
        }

        private static MainWindow.DeviceTypes? QuickConnect(string endpoint)
        {
            var appIdentifier = SimpleHttpServer.SendWhoAreYou(endpoint);
            if (String.IsNullOrWhiteSpace(appIdentifier)) return null;
            if (appIdentifier.Contains("Android")) return MainWindow.DeviceTypes.Android;
            if (appIdentifier.Contains("iOS")) return MainWindow.DeviceTypes.iOS;
            return null;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedDeviceItem = DevicesList.SelectedItem as MainWindow.DeviceItem;
            DialogResult = true;
            Close();
        }

        private void RunLocalButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedDeviceItem = new MainWindow.DeviceItem
            {
                DeviceAddress = "__LOCAL__",
                DeviceType = MainWindow.DeviceTypes.Local,
                DeviceName = "Local"
            };
            DialogResult = true;
            Close();
        }

        private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConnectButton.IsEnabled = true;
        }
    }
}
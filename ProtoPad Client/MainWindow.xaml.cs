using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using ActiproSoftware.Text.Languages.CSharp.Implementation;
using ActiproSoftware.Text.Languages.DotNet;
using ActiproSoftware.Text.Languages.DotNet.Ast.Implementation;
using ActiproSoftware.Text.Languages.DotNet.Reflection;
using ActiproSoftware.Text.Parsing;
using ActiproSoftware.Windows.Controls.SyntaxEditor.IntelliPrompt.Implementation;
using Microsoft.CSharp;
using ServiceDiscovery;
using mshtml;

namespace ProtoPad_Client
{    
    public partial class MainWindow
    {
        public const string CodeTemplateStatementsPlaceHolder = "__STATEMENTSHERE__";
        public const int multicastForwardedHostPort = 15353; // todo: auto-find available port
        public const int httpForwardedHostPort = 18080; // todo: auto-find available port

        private HTMLDivElementClass _htmlHolder;
        private IHTMLWindow2 _htmlWindow;
        private string _currentWrapText;
        private IProjectAssembly _projectAssembly;
        private UdpDiscoveryClient _udpDiscoveryClient;
        private readonly List<string> _referencedAssemblies = new List<string>();  

        private string WrapHeader
        {
            get { return _currentWrapText.Split(new[] { CodeTemplateStatementsPlaceHolder }, StringSplitOptions.None)[0]; }
        }
        private string WrapFooter
        {
            get { return _currentWrapText.Split(new[] { CodeTemplateStatementsPlaceHolder }, StringSplitOptions.None)[1]; }
        }

        private EditorHelpers.CodeType _currentCodeType = EditorHelpers.CodeType.Statements;
        private DeviceItem _currentDevice;

        public enum DeviceTypes {Android, iOS}

        public class DeviceItem
        {
            public string DeviceName { get; set; }
            public string DeviceAddress { get; set; }
            public DeviceTypes DeviceType;
        }

        public MainWindow()
        {
            InitializeComponent();            

            InitializeEditor();
            SearchForRunningProtoPadServers();
        }

        private async void SearchForRunningProtoPadServers()
        {
            //_currentDevice = new DeviceItem { DeviceAddress = "http://192.168.1.104:8080/", DeviceName = "Yarvik", DeviceType = DeviceTypes.Android };

            _udpDiscoveryClient = new UdpDiscoveryClient(
                ready => { },
                (name, address) => Dispatcher.Invoke(() =>
                    {
                        var deviceItem = new DeviceItem
                            {
                                DeviceAddress = address,
                                DeviceName = name,
                                DeviceType =
                                    name.StartsWith("ProtoPad Service on ANDROID Device ")
                                        ? DeviceTypes.Android
                                        : DeviceTypes.iOS
                            };
                        if (!DevicesComboBox.Items.Cast<object>().Any(i => (i as DeviceItem).DeviceAddress == deviceItem.DeviceAddress))
                        {
                            DevicesComboBox.Items.Add(deviceItem);
                        }
                        DevicesComboBox.IsEnabled = true;
                        Debug.WriteLine("Found '{0}' on {1}", name, address);
                    }));

            ResultTextBox.NavigateToString(Properties.Resources.ResultHtmlWrap);
            await ResultTextBox.GetNavigatedEventAsync("Navigated");
            var htmlDocument = ResultTextBox.Document as HTMLDocument;
            _htmlHolder = htmlDocument.getElementById("wrapallthethings") as HTMLDivElementClass;
            _htmlWindow = htmlDocument.parentWindow;

            _udpDiscoveryClient.SendServerPing();
            var ticksPassed = 0;
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += (s, a) =>
                {
                    if (ticksPassed > 2)
                    {
                        dispatcherTimer.Stop();
                        if (DevicesComboBox.Items.Count == 1) DevicesComboBox.SelectedIndex = 0;
                    }
                    _udpDiscoveryClient.SendServerPing();
                    ticksPassed++;
                };
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(200);
            dispatcherTimer.Start();
        }

        private void InitializeEditor()
        {
            // Initialize the project assembly (enables support for automated IntelliPrompt features)
            _projectAssembly = new CSharpProjectAssembly("ProtoPad Client");
            var assemblyLoader = new BackgroundWorker();
            assemblyLoader.DoWork += DotNetProjectAssemblyReferenceLoader;
            assemblyLoader.RunWorkerAsync();

            // Load the .NET Languages Add-on C# language and register the project assembly on it
            var language = new CSharpSyntaxLanguage();
            language.RegisterProjectAssembly(_projectAssembly);

            CodeEditor.Document.Language = language;

            CodeEditor.Document.Language.RegisterService(new IndicatorQuickInfoProvider());

            CodeEditor.PreviewKeyDown += (sender, args) =>
                {
                    if (args.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
                    SendCodeButton_Click(null, null);
                    args.Handled = true;
                };
        }

        private void DotNetProjectAssemblyReferenceLoader(object sender, DoWorkEventArgs e)
        {
            _projectAssembly.AssemblyReferences.AddMsCorLib();
            if (_currentDevice == null) return;
            (_currentDevice.DeviceType == DeviceTypes.Android ? EditorHelpers.StandardAssemblies_Android : EditorHelpers.StandardAssemblies_iOS).ToList().ForEach(a=>_projectAssembly.AssemblyReferences.AddFrom(a));
        }

        private void ConnectToAppButton_Click(object sender, RoutedEventArgs e)
        {
            DevicesComboBox.Items.Clear();
            _udpDiscoveryClient.SendServerPing();
        }

        private void SendCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                MessageBox.Show("Please connect to an app first!");
                return;
            }
            var result = SendCode(_currentDevice.DeviceAddress);
            if (result == null) return;
            var errorMessage = result.ErrorMessage;
            if (!String.IsNullOrWhiteSpace(errorMessage))
            {
                if (errorMessage.StartsWith("___EXCEPTION_____At line: "))
                {
                    var exceptionBody = errorMessage.Substring("___EXCEPTION_____At line: ".Length);
                    var exceptionParts = exceptionBody.Split(new[] {"__"}, StringSplitOptions.None);
                    var codeOffset = int.Parse(exceptionParts[0]) - 1;

                    var position = CodeEditor.Document.CurrentSnapshot.OffsetToPosition(codeOffset);

                    ShowLineError(position.Line, exceptionParts[1]);
                }
                else
                {
                    MessageBox.Show(errorMessage.Substring("___EXCEPTION___".Length));
                }
                _htmlHolder.innerHTML = errorMessage;
            }
            else if (result.Results != null)
            {
                _htmlHolder.innerHTML = String.Join("", result.Results.Select(r => "<h1>" + r.Item1 + "</h1>" + DumpToXhtml.Dump(r.Item2, 0).ToString()));
                _htmlWindow.execScript("Update();", "javascript");
            }
        }

        private void LoadAssemblyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                MessageBox.Show("Please connect to an app first!");
                return;
            }
            var dlg = new Microsoft.Win32.OpenFileDialog { DefaultExt = ".dll" };

            if (_currentDevice.DeviceType == DeviceTypes.Android)
            {
                dlg.Filter = "Xamarin.Android-compatible assembly (.dll)|*.dll";
                dlg.InitialDirectory = @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoAndroid\";
            }
            else
            {
                dlg.Filter = "Xamarin.iOS-compatible assembly (.dll)|*.dll";
                dlg.InitialDirectory = @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoTouch\v4.0\";
            }

            var result = dlg.ShowDialog();
            if (!result.Value) return;
            var assemblyPath = dlg.FileName;
            _projectAssembly.AssemblyReferences.AddFrom(assemblyPath);
            _referencedAssemblies.Add(assemblyPath);
            SimpleHttpServer.SendPostRequest(_currentDevice.DeviceAddress, File.ReadAllBytes(assemblyPath));
        }

        private ExecuteResponse SendCode(string url, bool wrapWithDefaultCode = true, string specialNonEditorCode = null)
        {
            var assemblyPath = CompileSource(wrapWithDefaultCode, specialNonEditorCode);
            var responseString = String.IsNullOrWhiteSpace(assemblyPath) ? null : SimpleHttpServer.SendPostRequest(url, File.ReadAllBytes(assemblyPath)).Trim();
            return String.IsNullOrWhiteSpace(responseString) ? null : UtilityMethods.JsonDecode<ExecuteResponse>(responseString);
        }

        private void VisitNodesAndSelectStatementOffsets(IAstNode node, ICollection<int> statementOffsets)
        {
            if (node is Statement && node.StartOffset.HasValue)
            {
                statementOffsets.Add(node.StartOffset.Value);
            }
            foreach (var childNode in node.Children)
            {
                VisitNodesAndSelectStatementOffsets(childNode, statementOffsets);
            }
        }

        private string GetSourceWithBreakPoints()
        {
            var parseData = CodeEditor.Document.ParseData as IDotNetParseData;
            var statementOffsets = new List<int>();
            VisitNodesAndSelectStatementOffsets(parseData.Ast, statementOffsets);
            var inserts = statementOffsets.ToDictionary(o => o, o => String.Format("____TrackStatementOffset({0});", o));
            return CodeEditor.Document.CurrentSnapshot.Text.InsertAll(inserts);
        }

        private string CompileSource(bool wrapWithDefaultCode, string specialNonEditorCode = null)
        {
            var codeWithOffsets = specialNonEditorCode ?? GetSourceWithBreakPoints().Replace("void Main(", "public void Main(");

            var sourceCode = wrapWithDefaultCode ? String.Format("{0}{1}{2}", WrapHeader.Replace("void Main(", "public void Main("), codeWithOffsets, WrapFooter) : codeWithOffsets;
            var cpd = new CSharpCodeProvider();
            var compilerParameters = new CompilerParameters();
            compilerParameters.ReferencedAssemblies.AddRange(_referencedAssemblies.ToArray());
            compilerParameters.GenerateExecutable = false;
            var compileResults = cpd.CompileAssemblyFromSource(compilerParameters, sourceCode);
            CodeEditor.Document.IndicatorManager.Clear<ErrorIndicatorTagger, ErrorIndicatorTag>();
            var errorStringBuilder = new StringBuilder();
            foreach (CompilerError error in compileResults.Errors)
            {
                var headerLineCount = WrapHeader.Split('\n').Length;
                var codeLineNumber = (error.Line - headerLineCount) / 2;
                ShowLineError(codeLineNumber, error.ErrorText);
                errorStringBuilder.AppendFormat("Error on line {0}: {1}\r\n", codeLineNumber, error.ErrorText);
            }
            if (!String.IsNullOrWhiteSpace(errorStringBuilder.ToString())) _htmlHolder.innerHTML = errorStringBuilder.ToString();
            return compileResults.Errors.Count > 0 ? null : compilerParameters.OutputAssembly;
        }

        private void ShowLineError(int codeLineNumber, string errorMessage)
        {
            if (codeLineNumber < 0 || codeLineNumber > CodeEditor.ActiveView.CurrentSnapshot.Lines.Count) codeLineNumber = 0;
            var editorLine = CodeEditor.ActiveView.CurrentSnapshot.Lines[codeLineNumber];
            CodeEditor.ActiveView.Selection.StartOffset = editorLine.StartOffset;
            CodeEditor.ActiveView.Selection.SelectToLineEnd();
            var tag = new ErrorIndicatorTag { ContentProvider = new PlainTextContentProvider(errorMessage) };
            CodeEditor.Document.IndicatorManager.Add<ErrorIndicatorTagger, ErrorIndicatorTag>(CodeEditor.ActiveView.Selection.SnapshotRange, tag);
        }        

        private void DevicesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _currentDevice = DevicesComboBox.SelectedItem as DeviceItem;
            if (_currentDevice == null) return;
            _currentDevice.DeviceAddress = _currentDevice.DeviceAddress;
            _htmlHolder.innerHTML = String.Format("Connected to device '{0}' on [{1}]", _currentDevice.DeviceName, _currentDevice.DeviceAddress);
            Title = String.Format("ProtoPad - {0}", DevicesComboBox.Text);

            _projectAssembly.AssemblyReferences.Clear();

            var assemblyLoader = new BackgroundWorker();
            assemblyLoader.DoWork += DotNetProjectAssemblyReferenceLoader;
            assemblyLoader.RunWorkerAsync();

            _referencedAssemblies.Clear();

            SetText();

            SendCodeButton.IsEnabled = true;
            LoadAssemblyButton.IsEnabled = true;

            if (_currentDevice.DeviceType == DeviceTypes.Android) return;

            var wrapText = EditorHelpers.GetWrapText(EditorHelpers.CodeType.Expression, _currentDevice.DeviceType);
            var getFolderCode = wrapText.Replace("__STATEMENTSHERE__", "Environment.GetFolderPath(Environment.SpecialFolder.Personal)");
            var result = SendCode(_currentDevice.DeviceAddress, false, getFolderCode);
            if (result == null) return;
            var folder = result.Results.FirstOrDefault();
            if (folder != null)
            {
                StatusLabel.Content = folder.Item2.PrimitiveValue.ToString();
            }
        }

        private void SetText()
        {
            if (_currentDevice == null) return;
            _currentWrapText = EditorHelpers.GetWrapText(_currentCodeType, _currentDevice.DeviceType);
            if (_currentDevice.DeviceType == DeviceTypes.Android)
            {
                EditorHelpers.StandardAssemblies_Android.ToList()
                             .ForEach(a => _referencedAssemblies.Add(a));
            }
            else
            {
                EditorHelpers.StandardAssemblies_iOS.ToList()
                             .ForEach(a => _referencedAssemblies.Add(a));
            }
            CodeEditor.Document.SetText(EditorHelpers.GetDefaultCode(_currentCodeType, _currentDevice.DeviceType));
            CodeEditor.Document.SetHeaderAndFooterText(WrapHeader, WrapFooter);
        }

        private void CodeTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            switch (CodeTypeComboBox.SelectedValue.ToString())
            {
                case "Expression":
                    _currentCodeType = EditorHelpers.CodeType.Expression;
                    break;
                case "Statements":
                    _currentCodeType = EditorHelpers.CodeType.Statements;
                    break;
                case "Program":
                    _currentCodeType = EditorHelpers.CodeType.Program;
                    break;
            }
            SetText();
        }

        private void ClearSimulatorWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null) return;
            var wrapText = EditorHelpers.GetWrapText(EditorHelpers.CodeType.Statements, _currentDevice.DeviceType);
            var clearCode = wrapText.Replace("__STATEMENTSHERE__", _currentDevice.DeviceType == DeviceTypes.iOS
                                                       ? "window.Subviews.ToList().ForEach(v=>v.RemoveFromSuperview());"
                                                       : ""); //todo: Android
            SendCode(_currentDevice.DeviceAddress, false, clearCode);            
        }

        private void AboutHelpButton_Click(object sender, RoutedEventArgs e)
        {
            (new AboutWindow()).Show();
        }

        private void AddManualIPButton_Click(object sender, RoutedEventArgs e)
        {
            var promptWindow = new PromptIPAddressWindow();
            if (!promptWindow.ShowDialog().Value) return;
            var address = String.Format("{0}:{1}", promptWindow.IPAddress, promptWindow.Port);
            var deviceType = QuickConnect(address);
            if (!deviceType.HasValue)
            {
                MessageBox.Show(String.Format("Could not connect to {0}", address));
                return;
            }
            var deviceItem = new DeviceItem
            {
                DeviceAddress = address,
                DeviceName = String.Format("{0} app on {1}", deviceType, address),
                DeviceType = deviceType.Value
            };
            if (!DevicesComboBox.Items.Cast<object>().Any(i => (i as DeviceItem).DeviceAddress == deviceItem.DeviceAddress))
            {
                DevicesComboBox.Items.Add(deviceItem);
            }
            DevicesComboBox.IsEnabled = true;
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
            var successResults = results.Where(r => r.Value.HasValue && r.Value.Value).Select(r => r.Key);
            if (successResults.Any())
            {
                MessageBox.Show(String.Format("Emulator(s) found at port(s) {0}, and configured successfully! Hit 'Find servers' to auto-discover your running app on this/these emulator(s).", String.Join(", ", successResults)));
                return;
            }
            var halfResults = results.Where(r => r.Value.HasValue && !r.Value.Value).Select(r => r.Key);
            if (halfResults.Any())
            {
                MessageBox.Show(String.Format("Emulator(s) found at port(s) {0}, but they may have already been configured, or were not able to be configured successfully. Please retry auto-discovery.", String.Join(", ", halfResults)));
                return;
            }
            var emptyResults = results.Where(r => !r.Value.HasValue).Select(r => r.Key);
            MessageBox.Show(String.Format("Emulator(s) found at port(s) {0}, but they could not be telnet-connected to. Please retry auto-discovery.", String.Join(", ", emptyResults)));
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

                    tc.WriteLine(String.Format("redir add udp:{0}:{1}", multicastForwardedHostPort, UdpDiscoveryServer.UdpServerPort));
                    udpCommandResponse = tc.Read();
                    tc.WriteLine(String.Format("redir add tcp:{0}:{1}", httpForwardedHostPort, 8080));
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

        private static DeviceTypes? QuickConnect(string endpoint)
        {
            var appIdentifier = SimpleHttpServer.SendWhoAreYou(endpoint);
            if (String.IsNullOrWhiteSpace(appIdentifier)) return null;
            if (appIdentifier.Contains("Android")) return DeviceTypes.Android;
            if (appIdentifier.Contains("iOS")) return DeviceTypes.iOS;
            return null;
        }

        private static IEnumerable<int> FindEmulatorPortCandidates()
        {
            var tcpRows = ManagedIpHelper.GetExtendedTcpTable(true);
            return (from tcpRow in tcpRows let process = Process.GetProcessById(tcpRow.ProcessId) where process != null where process.ProcessName.Contains("emulator-arm") select tcpRow.LocalEndPoint.Port).Distinct().ToList();
        }
    }
}
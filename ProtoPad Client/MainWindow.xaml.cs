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
using ActiproSoftware.Text;
using ActiproSoftware.Text.Implementation;
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

        private IHTMLElement _htmlHolder;
        private IHTMLWindow2 _htmlWindow;
        private string _currentWrapText;
        private IProjectAssembly _projectAssembly;
        
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

        public enum DeviceTypes {Android, iOS, Local}

        public class DeviceItem
        {
            public string DeviceName { get; set; }
            public string DeviceAddress { get; set; }
            public string MainXamarinAssemblyName { get; set; }
            public DeviceTypes DeviceType;
        }

        public MainWindow()
        {
            InitializeComponent();            

            InitializeEditor();
            InitializeResultWindow();
        }

        private void LogToResultsWindow(string message, params object[] stringFormatArguments)
        {
            if (_htmlHolder == null) return;
            var formattedMessage = String.Format(message, stringFormatArguments);
            Debug.WriteLine(formattedMessage);
            _htmlHolder.innerHTML = formattedMessage;
        }

        private void InitializeResultWindow()
        {
            ResultTextBox.Navigated += (sender, args) =>
            {
                var htmlDocument = ResultTextBox.Document as HTMLDocument;
                //var obj = htmlDocument.getElementById("wrapallthethings");
                _htmlHolder = htmlDocument.getElementById("wrapallthethings");
                _htmlWindow = htmlDocument.parentWindow;
            };
            ResultTextBox.NavigateToString(Properties.Resources.ResultHtmlWrap);
            //await ResultTextBox.GetNavigatedEventAsync("Navigated");
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
                    //if (args.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
                    if (args.Key != Key.F5) return;
                    SendCodeButton_Click(null, null);
                    args.Handled = true;
                };
        }

        private void DotNetProjectAssemblyReferenceLoader(object sender, DoWorkEventArgs e)
        {
            _projectAssembly.AssemblyReferences.AddMsCorLib();
            if (_currentDevice == null) return;
            (_currentDevice.DeviceType == DeviceTypes.Android ? 
                EditorHelpers.GetXamarinAndroidBaseAssemblies(_currentDevice.MainXamarinAssemblyName) : 
                EditorHelpers.GetXamariniOSBaseAssemblies(_currentDevice.MainXamarinAssemblyName))
                .ToList().ForEach(a => _projectAssembly.AssemblyReferences.AddFrom(a));
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
                if (errorMessage.StartsWith("___EXCEPTION_____At offset: "))
                {
                    var exceptionBody = errorMessage.Substring("___EXCEPTION_____At offset: ".Length);
                    var exceptionParts = exceptionBody.Split(new[] {"__"}, StringSplitOptions.None);
                    var codeOffset = int.Parse(exceptionParts[0]) - 1;

                    var position = CodeEditor.Document.CurrentSnapshot.OffsetToPosition(codeOffset);

                    ShowLineError(position.Line+1, exceptionParts[1]);
                }
                LogToResultsWindow(errorMessage);
            }
            else if (result.Results != null)
            {
                _htmlHolder.innerHTML = String.Join("", result.Results.Select(r => "<h1>" + r.ResultKey + "</h1>" + DumpToXhtml.Dump(r.ResultValue, 0).ToString()));
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

        private bool VisitNodesAndSelectStatementOffsets(IAstNode node, ICollection<int> statementOffsets)
        {
            if (node.Value == "SimpleName: \"DumpHelpers\"" && node.Parent.Value == "ClassDeclaration")
            {
                return false;
            }

            if (node is Statement && node.StartOffset.HasValue && node.StartOffset >= 0)
            {
                bool isDumpMethodStatement = false;
                if (node.Parent != null && node.Parent.Parent != null)
                {
                    var methodNode = node.Parent.Parent as MethodDeclaration;
                    if (methodNode != null)
                    {
                        if (methodNode.Name.Text == "____TrackStatementOffset") isDumpMethodStatement = true;
                    }
                }

                if (!isDumpMethodStatement) statementOffsets.Add(node.StartOffset.Value);
            }
            return node.Children.All(childNode => VisitNodesAndSelectStatementOffsets(childNode, statementOffsets));
        }

        private string GetSourceWithBreakPoints()
        {
            var parseData = CodeEditor.Document.ParseData as IDotNetParseData;
            //parseData.Snapshot.PositionRangeToTextRange(new TextPositionRange(new TextPosition()))
            
            var statementOffsets = new List<int>();
            VisitNodesAndSelectStatementOffsets(parseData.Ast, statementOffsets);


            var inserts = statementOffsets.ToDictionary(o => o, o => String.Format("____TrackStatementOffset({0});", o));

            var options = new TextChangeOptions { OffsetDelta = TextChangeOffsetDelta.SequentialOnly };
            var change = parseData.Snapshot.CreateTextChange(TextChangeTypes.Custom, options);
            foreach (var insert in inserts)
            {
                change.InsertText(insert.Key, insert.Value);
            }
            change.Apply();
            return change.Snapshot.Text;

            //return parseData.Snapshot.Text.InsertAll(inserts, parseData.Snapshot);
        }

        private string CompileSource(bool wrapWithDefaultCode, string specialNonEditorCode = null)
        {
            var codeWithOffsets = (specialNonEditorCode ?? GetSourceWithBreakPoints()).Replace("void Main(", "public void Main(");

            var sourceCode = wrapWithDefaultCode ? String.Format("{0}{1}{2}", WrapHeader.Replace("void Main(", "public void Main("), codeWithOffsets, WrapFooter) : codeWithOffsets;
            var provider_options = new Dictionary<string, string> {{"CompilerVersion", "v3.5"}};
            var cpd = new CSharpCodeProvider(provider_options);
            //var cpd = new CSharpCodeProvider();
            
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
            if (codeLineNumber < 0 || codeLineNumber >= CodeEditor.ActiveView.CurrentSnapshot.Lines.Count) codeLineNumber = 0;
            var editorLine = CodeEditor.ActiveView.CurrentSnapshot.Lines[codeLineNumber];
            CodeEditor.ActiveView.Selection.StartOffset = editorLine.StartOffset;
            CodeEditor.ActiveView.Selection.SelectToLineEnd();
            var tag = new ErrorIndicatorTag { ContentProvider = new PlainTextContentProvider(errorMessage) };
            CodeEditor.Document.IndicatorManager.Add<ErrorIndicatorTagger, ErrorIndicatorTag>(CodeEditor.ActiveView.Selection.SnapshotRange, tag);
        }        

        private void ConnectToApp(DeviceItem deviceItem)
        {
            if (deviceItem == null) return;
            _currentDevice = deviceItem;

            _currentDevice.MainXamarinAssemblyName = SimpleHttpServer.SendGetMainXamarinAssemblyName(_currentDevice.DeviceAddress);

            _currentDevice.DeviceAddress = _currentDevice.DeviceAddress;
            LogToResultsWindow("Connected to device '{0}' on [{1}]", _currentDevice.DeviceName, _currentDevice.DeviceAddress);
            Title = String.Format("ProtoPad - {0}", _currentDevice.DeviceName);

            _projectAssembly.AssemblyReferences.Clear();

            var assemblyLoader = new BackgroundWorker();
            assemblyLoader.DoWork += DotNetProjectAssemblyReferenceLoader;
            assemblyLoader.RunWorkerAsync();

            _referencedAssemblies.Clear();

            SetText();

            SendCodeButton.IsEnabled = true;
            LoadAssemblyButton.IsEnabled = true;

            StatusLabel.Content = "";

            if (_currentDevice.DeviceType == DeviceTypes.Android) 
            {
                return; // todo: locate and provide Android Emulator file path if applicable
            }

            var wrapText = EditorHelpers.GetWrapText(EditorHelpers.CodeType.Expression, _currentDevice.DeviceType);
            var getFolderCode = wrapText.Replace("__STATEMENTSHERE__", "Environment.GetFolderPath(Environment.SpecialFolder.Personal)");
            var result = SendCode(_currentDevice.DeviceAddress, false, getFolderCode);
            if (result == null || result.Results == null) return;
            var folder = result.Results.FirstOrDefault();
            if (folder == null) return;
            StatusLabel.Content = folder.ResultValue.PrimitiveValue.ToString();
        }

        private void SetText()
        {
            if (_currentDevice == null) return;
            _currentWrapText = EditorHelpers.GetWrapText(_currentCodeType, _currentDevice.DeviceType);
            if (_currentDevice.DeviceType == DeviceTypes.Android)
            {
                EditorHelpers.GetXamarinAndroidBaseAssemblies(_currentDevice.MainXamarinAssemblyName).ToList().ForEach(a => _referencedAssemblies.Add(a));
            }
            else
            {
                EditorHelpers.GetXamariniOSBaseAssemblies(_currentDevice.MainXamarinAssemblyName).ToList().ForEach(a => _referencedAssemblies.Add(a));
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

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var connectWindow = new ConnectWindow();
            if (connectWindow.ShowDialog().Value)
            {
                ConnectToApp(connectWindow.SelectedDeviceItem);
            }
        }
    }
}
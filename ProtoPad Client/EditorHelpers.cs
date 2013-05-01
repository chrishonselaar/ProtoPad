using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using ServiceDiscovery;

namespace ProtoPad_Client
{
    public static class EditorHelpers
    {
        public static ExecuteResponse ExecuteLoadedAssemblyString(byte[] loadedAssemblyBytes) // todo: provide special WPF controls result window
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
                printMethod.Invoke(loadedInstance, new object[] { }); // todo: provide special WPF controls result window
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

        private static DumpHelpers.DumpObj GetDumpObjectFromObject(object value)
        {
            var objType = value.GetType();
            var dumpObject = new DumpHelpers.DumpObj(Convert.ToString(objType.GetField("Description").GetValue(value)),
                objType.GetField("Value").GetValue(value),
                Convert.ToInt32(objType.GetField("Level").GetValue(value)),
                Convert.ToBoolean(objType.GetField("ToDataGrid").GetValue(value))
            );
            return dumpObject;
        }

        private const string DotNetTargetVersion = "4.0";
        private const string XamariniOSTargetsFile = @"$(MSBuildExtensionsPath)\Xamarin\iOS\Xamarin.MonoTouch.CSharp.targets";
        private const string XamarinAndroidTargetsFile = @"$(MSBuildExtensionsPath)\Xamarin\iOS\Xamarin.Android.CSharp.targets";
        private const string RegularCSharpTargetsFile = @"$(MSBuildToolsPath)\Microsoft.CSharp.targets";

        private static readonly string[] AndroidAssemblyNames = new[] { "mscorlib", "system", "system.core", "mono.android" };
        private static readonly string[] IOSAssemblyNames = new[] { "system", "system.core", "monotouch" };
        private static readonly string[] RegularAssemblyNames = new[] { "system", "system.core", "system.drawing" };

        private const string AssemblyResolverDummyProjectFileTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""{0}"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    {1}    
  </ItemGroup>
  <Import Project=""{2}"" />
</Project>";

        /// <summary>
        /// Reliably resolve basic Xamarin/Microsoft assembly paths by using MSBuild and a mock project file
        /// </summary>
        /// <param name="toolsVersion">4.0 currently</param>
        /// <param name="assemblyNames">list of basic assembly names (eg. "System.Core")</param>
        /// <param name="targetsPath">.targets file path</param>
        /// <returns></returns>
        private static List<string> GetResolvedAssemblies(string toolsVersion, IEnumerable<string> assemblyNames, string targetsPath)
        {
            var references = String.Join("\r\n", assemblyNames.Select(a => String.Format("<Reference Include=\"{0}\" />", a)));
            var projectFileContents = String.Format(AssemblyResolverDummyProjectFileTemplate, toolsVersion, references, targetsPath);
            var xmlReader = XmlReader.Create(new StringReader(projectFileContents));
            var projectRootElement = ProjectRootElement.Create(xmlReader);
            var projectInstance = new ProjectInstance(projectRootElement);
            projectInstance.SetProperty("BuildProjectReferences", "False");
            var buildRequestData = new BuildRequestData(projectInstance, new[] { "ResolveProjectReferences", "ResolveReferences" });
            BuildManager.DefaultBuildManager.Build(null, buildRequestData);
            return projectInstance.GetItems("ReferencePath").Select(referencePath => referencePath.EvaluatedInclude).ToList();
        }

        private static List<String> OrderAsInSourceList(IEnumerable<string> unorderedPathList, string[] sourcebaseFileNamesOrdered)
        {
            return unorderedPathList.OrderBy(item => Array.IndexOf(sourcebaseFileNamesOrdered, Path.GetFileNameWithoutExtension(item).ToLower())).ToList();
        }

        public static List<String> GetXamarinAndroidBaseAssemblies(string mainMonodroidAssemblyName, out string msCorLibPath)
        {
            
            var assemblyPaths = GetResolvedAssemblies(DotNetTargetVersion, AndroidAssemblyNames, XamarinAndroidTargetsFile);
            msCorLibPath = assemblyPaths.First(a => Path.GetFileName(a).Equals("mscorlib.dll", StringComparison.InvariantCultureIgnoreCase));
            var paths = assemblyPaths.Except(new[] {msCorLibPath}).ToList();
            return OrderAsInSourceList(paths, AndroidAssemblyNames);
        }

        public static List<String> GetXamariniOSBaseAssemblies(string mainMonotouchAssemblyName, out string msCorLibPath)
        {
            var assemblyPaths = GetResolvedAssemblies(DotNetTargetVersion, IOSAssemblyNames, XamariniOSTargetsFile);
            msCorLibPath = assemblyPaths.First(a => Path.GetFileName(a).Equals("mscorlib.dll", StringComparison.InvariantCultureIgnoreCase));
            var paths = assemblyPaths.Except(new[] { msCorLibPath }).ToList();
            return OrderAsInSourceList(paths, IOSAssemblyNames);
        }

        public static List<String> GetRegularDotNetBaseAssemblyNames()
        {
            var assemblyPaths = GetResolvedAssemblies(DotNetTargetVersion, RegularAssemblyNames, RegularCSharpTargetsFile);
            var msCorLibPath = assemblyPaths.First(a => Path.GetFileName(a).Equals("mscorlib.dll", StringComparison.InvariantCultureIgnoreCase));
            var paths = assemblyPaths.Except(new[] { msCorLibPath }).ToList();
            return OrderAsInSourceList(paths, RegularAssemblyNames);
        }

        public static string GetDeviceSpecificMainParams(MainWindow.DeviceTypes deviceType)
        {
            switch (deviceType)
            {
                case MainWindow.DeviceTypes.Android:
                    return "Activity activity, Android.Views.Window window";
                case MainWindow.DeviceTypes.iOS:
                    return "UIApplicationDelegate appDelegate, UIWindow window";
                default:
                case MainWindow.DeviceTypes.Local:
                    return "";
            }
        }

        public const string CodeTemplateStatementsPlaceHolder = "__STATEMENTSHERE__";

        public static string GetWrapText(MainWindow.CodeTypes codeType, MainWindow.DeviceTypes deviceType, List<string> extraUsingStatements)
        {
            var wrapText = WrapText_Base;

            switch (codeType)
            {
                case MainWindow.CodeTypes.Expression:
                    wrapText = wrapText.Replace("__STATEMENTSHERE__", @"void Main(" + GetDeviceSpecificMainParams(deviceType) + @")
{
    __STATEMENTSHERE__.Dump();
}");
                    break;
                case MainWindow.CodeTypes.Statements:
                    wrapText = wrapText.Replace("__STATEMENTSHERE__", @"void Main(" + GetDeviceSpecificMainParams(deviceType) + @")
{
    __STATEMENTSHERE__
}");
                    break;
                case MainWindow.CodeTypes.Program:
                    break;
            }

            var usingStatements = "";
            switch (deviceType)
            {
                case MainWindow.DeviceTypes.Android:
                    usingStatements = DefaultUsingStatements_Android;
                    break;
                case MainWindow.DeviceTypes.iOS:
                    usingStatements = DefaultUsingStatements_iOS;
                    break;
                case MainWindow.DeviceTypes.Local:
                    usingStatements = DefaultUsingStatements_RegularDotNet;
                    break;
            }
            wrapText = wrapText.Replace("__USINGS__", usingStatements + (extraUsingStatements == null ? "" : String.Join("\r\n", extraUsingStatements)));
            return wrapText + WrapTextDumpHelpers;
        }

        public static string GetDefaultCode(MainWindow.CodeTypes codeType, MainWindow.DeviceTypes deviceType)
        {
            var sampleStatements = "";
            switch (deviceType)
            {
                case MainWindow.DeviceTypes.Android:
                    sampleStatements = Properties.Resources.SampleCodeAndroidProgram;
                    break;
                case MainWindow.DeviceTypes.iOS:
                    sampleStatements = Properties.Resources.SampleCodeiOSProgram;
                    break;
                case MainWindow.DeviceTypes.Local:
                    sampleStatements = Properties.Resources.SampleCodeRegularProgram;
                    break;
            }

            switch (codeType)
            {
                case MainWindow.CodeTypes.Program:
                    sampleStatements = sampleStatements.Replace("\n", "\n\t");
                    return @"void Main(" + GetDeviceSpecificMainParams(deviceType) + @")
{
    " + sampleStatements + @"
}

// Define other methods and classes here";
                case MainWindow.CodeTypes.Expression:
                    return "";
                case MainWindow.CodeTypes.Statements:
                    return sampleStatements;
            }
            return "";
        }

        public const string DefaultUsingStatements_iOS = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreImage;
using MonoTouch.CoreGraphics;
using MonoTouch.AVFoundation;
using MonoTouch;";

        public const string DefaultUsingStatements_Android = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Threading;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using Java.Net;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using Android.OS;";

        public const string DefaultUsingStatements_RegularDotNet = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;";

        public const string ClearWindowStatements_iOS = "window.Subviews.ToList().ForEach(v=>v.RemoveFromSuperview());";
        public const string ClearWindowStatements_Android = @"var viewGroup = window.DecorView as ViewGroup;
var layout = viewGroup.GetChildAt(0) as LinearLayout;
var frame = layout.GetChildAt(1) as FrameLayout;
var childCount = frame.ChildCount;
for (int i = 0; i < childCount; i++)
{
	frame.RemoveViewAt(0);
}";

        public const string WrapText_Base = @"__USINGS__
	
public class __MTDynamicCode
{
    public static int ___lastExecutedStatementOffset = 0;
    public static void ____TrackStatementOffset(int offset)
    {
        ___lastExecutedStatementOffset = offset;
    }
    public static int ___maxEnumerableItemCount = 1000;
    public static List<DumpHelpers.DumpObj> ___dumps = new List<DumpHelpers.DumpObj>();

    __STATEMENTSHERE__    
}";

        public const string WrapTextDumpHelpers = @"

public static class DumpHelpers
{
    public const int DefaultLevel = 3;

    public class DumpObj
    {
        public string Description;
        public object Value;
        public int Level;
        public bool ToDataGrid;

        public DumpObj(string description, object value, int level, bool toDataGrid)
        {
            Description = description;
            Value = value;
            Level = level;
            ToDataGrid = toDataGrid;
        }
    }

    public static void Dump(this object dumpValue)
    {
        __MTDynamicCode.___dumps.Add(new DumpObj("""", dumpValue, DefaultLevel, false));
    }
    public static void Dump(this object dumpValue, string description)
    {
        __MTDynamicCode.___dumps.Add(new DumpObj(description, dumpValue, DefaultLevel, false));
    }
    public static void Dump(this object dumpValue, int depth)
    {
        __MTDynamicCode.___dumps.Add(new DumpObj("""", dumpValue, depth, false));
    }
    public static void Dump(this object dumpValue, bool toDataGrid)
    {
        __MTDynamicCode.___dumps.Add(new DumpObj("""", dumpValue, DefaultLevel, toDataGrid));
    }
    public static void Dump(this object dumpValue, string description, int depth)
    {
        __MTDynamicCode.___dumps.Add(new DumpObj(description, dumpValue, depth, false));
    }
    public static void Dump(this object dumpValue, string description, bool toDataGrid)
    {
        __MTDynamicCode.___dumps.Add(new DumpObj(description, dumpValue, DefaultLevel, toDataGrid));
    }
}";
    }
}

public static class UtilityMethods
{
    public static T JsonDecode<T>(string jsonValue)
    {
        if (String.IsNullOrWhiteSpace(jsonValue)) return default(T);
        var serializer = new DataContractJsonSerializer(typeof(T));
        T result;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonValue)))
        {
            result = (T)serializer.ReadObject(stream);
            stream.Close();
        }
        return result;
    }
}
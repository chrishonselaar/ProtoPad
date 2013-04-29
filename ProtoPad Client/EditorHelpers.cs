using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using ActiproSoftware.Text;

namespace ProtoPad_Client
{
    public static class EditorHelpers
    {
        public enum CodeType {Expression, Statements, Program}

        public static string GetFrameworkReferenceAssembliesDirectory()
        {
            var programFilesx86Dir = Environment.GetEnvironmentVariable("programfiles(x86)") ?? @"C:\Program Files (x86)\";
            return Path.Combine(programFilesx86Dir, @"Reference Assemblies\Microsoft\Framework");
        }

        public static string[] GetXamarinAndroidBaseAssemblies(string mainMonodroidAssemblyName)
        {
            if (String.IsNullOrWhiteSpace(mainMonodroidAssemblyName)) return new string[]{};
            var fileNames = Directory.GetFiles(GetFrameworkReferenceAssembliesDirectory(), "Mono.Android.dll", SearchOption.AllDirectories);
            //var mainMonodroidAssemblyPath = fileNames.First(f => Assembly.LoadFrom(f). .FullName == mainMonodroidAssemblyName);
            const string mainMonodroidAssemblyPath = @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoAndroid\v4.0\Mono.Android.dll";
            var monoDroidAssembliesParentPath = Directory.GetParent(mainMonodroidAssemblyPath).Parent.FullName;
            var androidSystemDllPath = Directory.GetFiles(monoDroidAssembliesParentPath, "System.dll", SearchOption.AllDirectories).First();
            //var androidMsCorDllPath = Directory.GetFiles(monoDroidAssembliesParentPath, "mscorlib.dll", SearchOption.AllDirectories).First();
            var androidSystemCoreDllPath = Directory.GetFiles(monoDroidAssembliesParentPath, "System.Core.dll", SearchOption.AllDirectories).First();
            return new[] { androidSystemDllPath, androidSystemCoreDllPath, /*androidMsCorDllPath, */mainMonodroidAssemblyPath };
        }

        public static string[] GetXamariniOSBaseAssemblies(string mainMonotouchAssemblyName)
        {
            if (String.IsNullOrWhiteSpace(mainMonotouchAssemblyName)) return new string[] { };
            var fileNames = Directory.GetFiles(GetFrameworkReferenceAssembliesDirectory(), "monotouch.dll", SearchOption.AllDirectories);
            var mainMonotouchAssemblyPath = fileNames.First(f => Assembly.LoadFrom(f).FullName == mainMonotouchAssemblyName);
            var monoTouchAssembliesParentPath = Directory.GetParent(mainMonotouchAssemblyPath).Parent.FullName;
            var monoTouchSystemDllPath = Directory.GetFiles(monoTouchAssembliesParentPath, "System.dll", SearchOption.AllDirectories).First();
            var monoTouchSystemCoreDllPath = Directory.GetFiles(monoTouchAssembliesParentPath, "System.Core.dll", SearchOption.AllDirectories).First();
            return new[] { mainMonotouchAssemblyPath, monoTouchSystemDllPath, monoTouchSystemCoreDllPath };
        }

        public static string[] GetRegularDotNetBaseAssemblies()
        {
            var systemCore = Assembly.Load("System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089").Location;
            var system = Assembly.Load("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089").Location;
            return new[] { systemCore, system };
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

        public static string GetWrapText(CodeType codeType, MainWindow.DeviceTypes deviceType)
        {
            string wrapText;
            switch (deviceType)
            {
                case MainWindow.DeviceTypes.Android:
                    wrapText = WrapText_Android_Base;
                    break;
                case MainWindow.DeviceTypes.iOS:
                    wrapText = WrapText_IOS_Base;
                    break;
                default:
                case MainWindow.DeviceTypes.Local:
                    wrapText = WrapText_RegularDotNet_Base;
                    break;
            }
            switch (codeType)
            {
                case CodeType.Expression:
                    wrapText = wrapText.Replace("__STATEMENTSHERE__", @"void Main("+GetDeviceSpecificMainParams(deviceType)+@")
{
    __STATEMENTSHERE__.Dump();
}");
                    break;
                case CodeType.Statements:
                    wrapText = wrapText.Replace("__STATEMENTSHERE__", @"void Main("+GetDeviceSpecificMainParams(deviceType)+@")
{
    __STATEMENTSHERE__
}");
                    break;
                case CodeType.Program:
                    break;
            }
            return wrapText + WrapTextDumpHelpers;
        }

        public static string GetDefaultCode(CodeType codeType, MainWindow.DeviceTypes deviceType)
        {
            string sampleStatements;
            switch (deviceType)
            {
                case MainWindow.DeviceTypes.Android:
                    sampleStatements = Properties.Resources.SampleCodeAndroidProgram;
                    break;
                case MainWindow.DeviceTypes.iOS:
                    sampleStatements = Properties.Resources.SampleCodeiOSProgram;
                    break;
                default:
                case MainWindow.DeviceTypes.Local:
                    sampleStatements = Properties.Resources.SampleCodeRegularProgram;
                    break;
            }

            sampleStatements = sampleStatements.Replace("\n", "\n\t");

            switch (codeType)
            {
                case CodeType.Program:
                    return @"void Main(" + GetDeviceSpecificMainParams(deviceType) + @")
{
    " + sampleStatements + @"
}

// Define other methods and classes here";
                default:
                case CodeType.Expression:
                    return "";
                case CodeType.Statements:
                    return sampleStatements;
            }
        }

        public const string WrapText_IOS_Base = @"using MonoTouch.UIKit;
using System;
using MonoTouch.Foundation;
using MonoTouch.CoreImage;
using MonoTouch.CoreGraphics;
using MonoTouch.AVFoundation;
using MonoTouch;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
	
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

        public const string WrapText_Android_Base = @"using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Threading;
using System.IO;
using Android.App;
using Android.Content;
using Java.Net;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using Android.OS;
using System.Runtime.CompilerServices;
	
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


        public const string WrapText_RegularDotNet_Base = @"using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Runtime.CompilerServices;
	
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
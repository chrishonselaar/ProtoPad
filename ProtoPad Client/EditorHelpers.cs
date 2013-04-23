using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace ProtoPad_Client
{
    public static class EditorHelpers
    {
        public static readonly string[] StandardAssemblies_Android = new[]
            {
                @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoAndroid\v1.0\System.dll",
                @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoAndroid\v4.0.3\Mono.Android.dll",
                @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoAndroid\v1.0\System.Core.dll"
            };

        public static readonly string[] StandardAssemblies_iOS = new[]
            {
                @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoTouch\v4.0\System.dll",
                @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoTouch\v4.0\System.Core.dll",
                @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\MonoTouch\v4.0\monotouch.dll"
            };

        public const string SampleCode_Android_Program = @"";

        public enum CodeType {Expression, Statements, Program}

        public static string GetDeviceSpecificMainParams(MainWindow.DeviceTypes deviceType)
        {
            return deviceType == MainWindow.DeviceTypes.Android ? "ViewGroup window" : "UIWindow window";
        }

        public static string GetWrapText(CodeType codeType, MainWindow.DeviceTypes deviceType)
        {
            var wrapText = deviceType == MainWindow.DeviceTypes.Android ? WrapText_Android_Base : WrapText_IOS_Base;
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
            switch (codeType)
            {
                case CodeType.Program:
                    var code = deviceType == MainWindow.DeviceTypes.Android
                                   ? SampleCode_Android_Program
                                   : Properties.Resources.SampleCodeiOSProgram;
                    code = code.Replace("\n", "\n\t");
                    return @"void Main(" + GetDeviceSpecificMainParams(deviceType) + @")
{
    " + code + @"
}

// Define other methods and classes here";
                default:
                case CodeType.Expression:
                case CodeType.Statements:
                    return "";
            }
        }

        public static string InsertAll(this string source, Dictionary<int, string> insertStrings)
        {
            if (!insertStrings.Any()) return source;
            var orderedInsertStrings = insertStrings.OrderBy(s => s.Key);
            var substrings = new List<string>();

            var first = orderedInsertStrings.First();
            if (first.Key > 0)
            {
                substrings.Add(source.Substring(0, first.Key));
            }

            var lastIndex = -1;
            for (var i = 0; i < orderedInsertStrings.Count(); i++)
            {
                var insertString = orderedInsertStrings.ElementAt(i);
                var start = insertString.Key;
                if (start < 0 || start >= source.Length) continue;
                var end = i < insertStrings.Count - 1 ? orderedInsertStrings.ElementAt(i + 1).Key : source.Length;
                substrings.Add(source.Substring(start, 1));
                substrings.Add(insertString.Value);
                lastIndex = end;
                substrings.Add(source.Substring(start + 1, end - (start + 1)));
            }

            if (lastIndex < source.Length)
            {
                if (lastIndex < 0) lastIndex = 0;
                substrings.Add(source.Substring(lastIndex, source.Length - lastIndex));
            }

            return String.Join("", substrings);
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
    public static List<Tuple<string, object, int, bool>> ___dumps = new List<Tuple<string, object, int, bool>>();

    __STATEMENTSHERE__    
}";

        public const string WrapText_Android_Base = @"using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Drawing;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
	
public class __MTDynamicCode
{
    public static int ___lastExecutedLineNumber = 1;
    public static List<Tuple<string, object, int, bool>> ___dumps = new List<Tuple<string, object, int, bool>>();

    __STATEMENTSHERE__    
}";

        public const string WrapTextDumpHelpers = @"

public static class DumpHelpers
{
    public const int DefaultLevel = 3;
    public static void Dump(this object dumpValue)
    {
        __MTDynamicCode.___dumps.Add(Tuple.Create<string, object, int, bool>("""", dumpValue, DefaultLevel, false));
    }
    public static void Dump(this object dumpValue, string description)
    {
        __MTDynamicCode.___dumps.Add(Tuple.Create<string, object, int, bool>(description, dumpValue, DefaultLevel, false));
    }
    public static void Dump(this object dumpValue, int depth)
    {
        __MTDynamicCode.___dumps.Add(Tuple.Create<string, object, int, bool>("""", dumpValue, depth, false));
    }
    public static void Dump(this object dumpValue, bool toDataGrid)
    {
        __MTDynamicCode.___dumps.Add(Tuple.Create<string, object, int, bool>("""", dumpValue, DefaultLevel, toDataGrid));
    }
    public static void Dump(this object dumpValue, string description, int depth)
    {
        __MTDynamicCode.___dumps.Add(Tuple.Create<string, object, int, bool>(description, dumpValue, depth, false));
    }
    public static void Dump(this object dumpValue, string description, bool toDataGrid)
    {
        __MTDynamicCode.___dumps.Add(Tuple.Create<string, object, int, bool>(description, dumpValue, DefaultLevel, toDataGrid));
    }
}";
    }
}

public static class UtilityMethods
{
    public static Task<T> GetEventAsync<T>(this object eventSource, string eventName) where T : EventArgs
    {
        var tcs = new TaskCompletionSource<T>();

        var type = eventSource.GetType();
        var ev = type.GetEvent(eventName);

        EventHandler handler = null;
        handler = delegate(object sender, EventArgs e)
            {
                ev.RemoveEventHandler(eventSource, handler);
                tcs.SetResult((T)e);
            };

        ev.AddEventHandler(eventSource, handler);
        return tcs.Task;
    }

    public static Task<NavigationEventArgs> GetNavigatedEventAsync(this object eventSource, string eventName)
    {
        var tcs = new TaskCompletionSource<NavigationEventArgs>();

        var type = eventSource.GetType();
        var ev = type.GetEvent(eventName);

        NavigatedEventHandler handler = null;
        handler = delegate(object sender, NavigationEventArgs e)
        {
            ev.RemoveEventHandler(eventSource, handler);
            tcs.SetResult(e);
        };

        ev.AddEventHandler(eventSource, handler);
        return tcs.Task;
    }

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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace ProtoPadServerLibrary_iOS
{
    public class DumpValue
    {
        public enum DumpTypes { PrimitiveEnumerable, ComplexEnumerable, Group, Primitive, Complex, BeyondMaxLevel, Image }

        public string TypeName { get; set; }
        public DumpTypes DumpType { get; set; }

        public object PrimitiveValue { get; set; } // only value types
        public Dictionary<string, DumpValue> ComplexValue { get; set; }

        public List<object> PrimitiveEnumerable { get; set; } // only value types
        public List<DumpValue> ComplexEnumerable { get; set; }

        /// <param name="primitiveValue"> must be a value type!!</param>
        public static DumpValue AsPrimitiveValue(object primitiveValue)
        {
            return new DumpValue { TypeName = primitiveValue.GetType().Name, DumpType = DumpTypes.Primitive, PrimitiveValue = primitiveValue };
        }

        /// <param name="primitiveEnumerable"> must be a list of value type!!</param>
        public static DumpValue AsPrimitiveEnumerable(List<object> primitiveEnumerable, string typeName)
        {
            return new DumpValue { TypeName = typeName, DumpType = DumpTypes.PrimitiveEnumerable, PrimitiveEnumerable = primitiveEnumerable };
        }

        public void AddComplexFieldValue(string fieldName, DumpValue fieldValue)
        {
            ComplexValue.Add(fieldName, fieldValue);
        }

        public static DumpValue AsComplexValue(string typeName)
        {
            return new DumpValue { TypeName = typeName, DumpType = DumpTypes.Complex, ComplexValue = new Dictionary<string, DumpValue>() };
        }

        public static DumpValue AsComplexEnumerable(List<DumpValue> complexEnumerable, string typeName)
        {
            return new DumpValue { TypeName = typeName, DumpType = DumpTypes.ComplexEnumerable, ComplexEnumerable = complexEnumerable };
        }

        public static DumpValue AsBeyondMaxLevel(string typeName)
        {
            return new DumpValue { TypeName = typeName, DumpType = DumpTypes.BeyondMaxLevel };
        }

        public static DumpValue AsImage(UIImage image)
        {
            var data = image.AsJPEG(0.6f);
            var dataBytes = new byte[data.Length];
            System.Runtime.InteropServices.Marshal.Copy(data.Bytes, dataBytes, 0, Convert.ToInt32(data.Length));
            return new DumpValue { TypeName = "___IMAGE___", DumpType = DumpTypes.Image, PrimitiveValue = Convert.ToBase64String(dataBytes) };
        }
    }

    public class Dumper
    {
        public static DumpValue ObjectToDumpValue(object viewModel, int maxDepth = 2, int maxEnumerableItemCount = 1000)
        {
            if (viewModel is UIImage)
            {
                return DumpValue.AsImage(viewModel as UIImage);
            } 
            return DumpObjectRecursive(viewModel, maxDepth, 0, maxEnumerableItemCount);
        }

        private static DumpValue DumpObjectRecursive(object sourceValue, int maxDepth, int currentDepth, int maxEnumerableItemCount)
        {
            if (sourceValue == null) return null;
            var modelType = sourceValue.GetType();

            if (modelType.IsValueType || modelType == typeof(string))
            {
                if (sourceValue is IntPtr) return null; // todo: pointers altijd negeren?
                return DumpValue.AsPrimitiveValue(sourceValue);
            }

            if (currentDepth > maxDepth) return DumpValue.AsBeyondMaxLevel(modelType.Name);

            var isGenericEnumerable = false;
            try
            {
                isGenericEnumerable = modelType.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition())
                    .Any(i => i == typeof(IEnumerable<>));
            }
            catch { }
            if (isGenericEnumerable)
            {
                var items = (IEnumerable)sourceValue;
                var valueList = items.Cast<object>().Take(maxEnumerableItemCount).Select(item => DumpObjectRecursive(item, maxDepth, currentDepth + 1, maxEnumerableItemCount)).Where(v => v != null).ToList();
                if (valueList.Any())
                {
                    var firstValue = valueList.First();
                    return firstValue.DumpType == DumpValue.DumpTypes.Primitive
                                ? DumpValue.AsPrimitiveEnumerable(valueList.Select(v => v.PrimitiveValue).ToList(), modelType.Name)
                                : DumpValue.AsComplexEnumerable(valueList, modelType.Name);
                }
            }
            else if (modelType == typeof(IEnumerable))
            {
                var items = (IEnumerable)sourceValue;
                return DumpValue.AsComplexEnumerable(items.Cast<object>().Take(maxEnumerableItemCount).Select(v => DumpObjectRecursive(v, maxDepth, currentDepth + 1, maxEnumerableItemCount)).ToList(), modelType.Name);
            }

            var complexValue = DumpValue.AsComplexValue(modelType.Name);

            var fields = modelType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                ProcessFieldOrProperty(sourceValue, maxDepth, currentDepth, field, null, complexValue, maxEnumerableItemCount);
            }
            var properties = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var property in properties)
            {
                ProcessFieldOrProperty(sourceValue, maxDepth, currentDepth, null, property, complexValue, maxEnumerableItemCount);
            }

            return complexValue.ComplexValue.Any() ? complexValue : null;
        }

        private static void ProcessFieldOrProperty(object sourceValue, int maxDepth, int currentDepth, FieldInfo field, PropertyInfo property, DumpValue complexValue, int maxEnumerableItemCount)
        {
            object fieldValue;
            try
            {
                fieldValue = property == null ? field.GetValue(sourceValue) : property.GetValue(sourceValue, null);
            }
            catch
            {
                return;
            }
            var fieldName = property == null ? field.Name : property.Name;
            if (fieldValue == null) return;

            var isNsObjectDescendant = sourceValue is NSObject;
            if ((fieldName == "Description" || fieldName == "DebugDescription" || fieldName == "RetainCount") && isNsObjectDescendant) return;

            var value = DumpObjectRecursive(fieldValue, maxDepth, currentDepth + 1, maxEnumerableItemCount);
            if (value != null) complexValue.AddComplexFieldValue(fieldName, value);
        }
    }
}
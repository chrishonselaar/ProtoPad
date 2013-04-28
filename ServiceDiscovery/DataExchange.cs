using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ServiceDiscovery
{
    [DataContract]
    public class ResultPair
    {
        [DataMember]
        public string ResultKey;
        [DataMember]
        public DumpValue ResultValue;

        public ResultPair(string resultKey, DumpValue resultValue)
        {
            ResultKey = resultKey;
            ResultValue = resultValue;
        }
    }

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
    }

	[DataContract]
	public class ExecuteResponseSerialize
	{
		[DataMember]
		public string ErrorMessage { get; set; }
		[DataMember]
		public List<ResultPair> Results { get; set; }
	}

	[DataContract]
    public class ExecuteResponse
    {
		[DataMember]
        public string ErrorMessage { get; set; }
		[DataMember]
        public List<ResultPair> Results { get; set; }

		[IgnoreDataMember]
        private List<DumpHelpers.DumpObj> DumpValues;
		[IgnoreDataMember]
        private int MaxEnumerableItemCount;

        public void SetMaxEnumerableItemCount(int maxEnumerableItemCount)
        {
            MaxEnumerableItemCount = maxEnumerableItemCount;
        }

        public int GetMaxEnumerableItemCount()
        {
            return MaxEnumerableItemCount;
        }

        public void SetDumpValues(List<DumpHelpers.DumpObj> dumpValues)
        {
            DumpValues = dumpValues;
        }

        public List<DumpHelpers.DumpObj> GetDumpValues()
        {
            return DumpValues;
        }
    }

	[DataContract]
    public class DumpValue
    {
        public enum DumpTypes { PrimitiveEnumerable, ComplexEnumerable, Group, Primitive, Complex, BeyondMaxLevel, Image }

		[DataMember]
        public string TypeName { get; set; }
		[DataMember]
        public DumpTypes DumpType { get; set; }
		[DataMember]
        public object PrimitiveValue { get; set; } // only value types
		[DataMember]
        public Dictionary<string, DumpValue> ComplexValue { get; set; }
		[DataMember]
        public List<object> PrimitiveEnumerable { get; set; } // only value types
		[DataMember]
		public List<DumpValue> ComplexEnumerable { get; set; }

        public void AddComplexFieldValue(string fieldName, DumpValue fieldValue)
        {
            ComplexValue.Add(fieldName, fieldValue);
        }
    }    
}
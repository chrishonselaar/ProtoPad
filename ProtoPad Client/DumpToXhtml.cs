using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;

namespace ProtoPad_Client
{
    [DataContract]
    public class DumpValue
    {
        public enum DumpTypes { PrimitiveEnumerable, ComplexEnumerable, Group, Primitive, Complex, BeyondMaxLevel, Image }

        public string TypeName { get; set; }
        public DumpTypes DumpType { get; set; }

        public object PrimitiveValue { get; set; } // only value types
        public Dictionary<string, DumpValue> ComplexValue { get; set; }

        public List<object> PrimitiveEnumerable { get; set; } // only value types
        public List<DumpValue> ComplexEnumerable { get; set; }
    }

    [DataContract]
    public class ExecuteResponse
    {
        public string ErrorMessage { get; set; }
        public List<Tuple<string, DumpValue>> Results { get; set; }
    }

    public static class DumpToXhtml
    {
        public static XNode Dump(DumpValue dumpValue, int level)
        {
            if (dumpValue == null) return new XText("");
            switch (dumpValue.DumpType)
            {
                case DumpValue.DumpTypes.Primitive:
                    return new XText(dumpValue.PrimitiveValue.ToString());
                case DumpValue.DumpTypes.Image:
                    var dataURI = String.Format("data:image/jpg;base64,{0}", dumpValue.PrimitiveValue);
                    return new XElement("img", new XAttribute("src", dataURI));
                case DumpValue.DumpTypes.BeyondMaxLevel:
                    return new XElement("div", new XAttribute("class", "beyondmaxlevel"), dumpValue.TypeName);
                case DumpValue.DumpTypes.Complex:
                    return new XElement("table", new XAttribute("data-level", level),
                        new XElement("thead", 
                            new XElement("tr", new XElement("td", new XElement("div", new XAttribute("class", "leftarrow"), " "), new XElement("span", dumpValue.TypeName), new XAttribute("colspan", 2)))), 
                        new XElement("tbody", dumpValue.ComplexValue.Select(v =>
                            new XElement("tr", new XElement("th", v.Key), new XElement("td", Dump(v.Value, level+1))))));
                case DumpValue.DumpTypes.ComplexEnumerable:
                    var allKeys = dumpValue.ComplexEnumerable.Where(v=>v.ComplexValue != null).SelectMany(v => v.ComplexValue.Select(c => c.Key)).Distinct().ToList();
                    return new XElement("table", new XAttribute("data-level", level), allKeys.Count > 10 ? new XAttribute("class", "collapsed") : null,
                        new XElement("thead",
                            new XElement("tr", new XElement("td", new XElement("div", new XAttribute("class", "leftarrow"), " "), new XElement("span", String.Format("{0} ({1} item{2})", dumpValue.TypeName,
                                dumpValue.ComplexEnumerable.Count, dumpValue.ComplexEnumerable.Count == 1 ? "" : "s")), new XAttribute("colspan", allKeys.Count))),
                            new XElement("tr", allKeys.Select(v => new XElement("th", v)))),
                        new XElement("tbody", dumpValue.ComplexEnumerable.Select(v =>
                            new XElement("tr", allKeys.Select(v2 => new XElement("td", v.ComplexValue.ContainsKey(v2) ? 
                                Dump(v.ComplexValue[v2], level + 1) : 
                                new XElement("span", new XAttribute("class", "null"), "NULL")))))));
                case DumpValue.DumpTypes.PrimitiveEnumerable:
                    return new XElement("table", new XAttribute("data-level", level),
                        new XElement("thead",
                            new XElement("tr", new XElement("td", new XElement("div", new XAttribute("class", "leftarrow"), " "), new XElement("span", String.Format("{0} ({1} item{2})", dumpValue.TypeName,
                                dumpValue.PrimitiveEnumerable.Count, dumpValue.PrimitiveEnumerable.Count == 1 ? "" : "s")))), 
                        new XElement("tbody", dumpValue.PrimitiveEnumerable.Select(v => 
                            new XElement("tr", new XElement("td", v))))));
            }
            return null;
        }
    }
}
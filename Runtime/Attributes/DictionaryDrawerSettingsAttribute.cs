using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    [Conditional("UNITY_EDITOR")]
    public class DictionaryDrawerSettingsAttribute : Attribute
    {
        public bool AlwaysExpanded { get; set; }
        public bool ShowElementLabels { get; set; }
        public int MaxItemPerPage { get; set; } = 50;
    }
}
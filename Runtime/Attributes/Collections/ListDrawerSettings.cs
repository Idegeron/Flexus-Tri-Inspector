using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    [Conditional("UNITY_EDITOR")]
    public class ListDrawerSettingsAttribute : Attribute
    {
        public bool Draggable { get; set; } = true;
        public bool HideAddButton { get; set; }
        public bool HideRemoveButton { get; set; }
        public bool AlwaysExpanded { get; set; }
        public bool AlwaysElementsExpanded { get; set; }
        public bool ShowElementLabels { get; set; }
        public int MaxItemPerPage { get; set; } = 50;
        public bool ShowDefaultBackground { get; set; } = true;
        public bool ShowAlternatingBackground { get; set; } = true;
    }
}

using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    [Conditional("UNITY_EDITOR")]
    public class DrawWithUnityAttribute : Attribute
    {
        public bool WithUiToolkit { get; set; }
    }
}
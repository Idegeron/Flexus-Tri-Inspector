﻿using System;
using TriInspector;
using TriInspector.Drawers;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

[assembly: RegisterTriValueDrawer(typeof(IntegerDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(LongDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(BooleanDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(FloatDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(StringDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(ColorDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(Color32Drawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(LayerMaskDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(EnumDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(Vector2Drawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(Vector3Drawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(Vector4Drawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(RectDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(AnimationCurveDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(BoundsDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(GradientDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(Vector2IntDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(Vector3IntDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(RectIntDrawer), TriDrawerOrder.Fallback)]
[assembly: RegisterTriValueDrawer(typeof(BoundsIntDrawer), TriDrawerOrder.Fallback)]

namespace TriInspector.Drawers
{
    public class StringDrawer : BuiltinDrawerBase<string>
    {
        protected override string OnValueGUI(Rect position, GUIContent label, string value)
        {
            return EditorGUI.TextField(position, label, value);
        }
    }

    public class BooleanDrawer : BuiltinDrawerBase<bool>
    {
        protected override bool OnValueGUI(Rect position, GUIContent label, bool value)
        {
            return EditorGUI.Toggle(position, label, value);
        }
    }

    public class IntegerDrawer : BuiltinDrawerBase<int>
    {
        protected override int OnValueGUI(Rect position, GUIContent label, int value)
        {
            return EditorGUI.IntField(position, label, value);
        }
    }

    public class LongDrawer : BuiltinDrawerBase<long>
    {
        protected override long OnValueGUI(Rect position, GUIContent label, long value)
        {
            return EditorGUI.LongField(position, label, value);
        }
    }

    public class FloatDrawer : BuiltinDrawerBase<float>
    {
        protected override float OnValueGUI(Rect position, GUIContent label, float value)
        {
            return EditorGUI.FloatField(position, label, value);
        }
    }

    public class ColorDrawer : BuiltinDrawerBase<Color>
    {
        protected override Color OnValueGUI(Rect position, GUIContent label, Color value)
        {
            return EditorGUI.ColorField(position, label, value);
        }
    }

    public class Color32Drawer : BuiltinDrawerBase<Color32>
    {
        protected override Color32 OnValueGUI(Rect position, GUIContent label, Color32 value)
        {
            return EditorGUI.ColorField(position, label, value);
        }
    }

    public class LayerMaskDrawer : BuiltinDrawerBase<LayerMask>
    {
        protected override LayerMask OnValueGUI(Rect position, GUIContent label, LayerMask value)
        {
            var mask = InternalEditorUtility.LayerMaskToConcatenatedLayersMask(value);
            var layers = InternalEditorUtility.layers;

            position = EditorGUI.PrefixLabel(position, label);
            return EditorGUI.MaskField(position, mask, layers);
        }
    }

    public class EnumDrawer : TriValueDrawer<Enum>
    {
        public override void OnGUI(Rect position, TriValue<Enum> propertyValue, TriElement next)
        {
            var value = propertyValue.SmartValue;
            var valueName = value != null ? value.ToString() : "[None]";
            var valueNameContent = new GUIContent(valueName);

            if (!propertyValue.Property.IsArrayElement && 
                !propertyValue.Property.TryGetAttribute<HideLabelAttribute>(out var hideLabelAttribute))
            {
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), propertyValue.Property.DisplayNameContent);
            }

            if (EditorGUI.DropdownButton(position, valueNameContent, FocusType.Passive))
            {
                var dropdown = new EnumDropDown(propertyValue, new AdvancedDropdownState());
                
                dropdown.Show(position);

                Event.current.Use();
            }
        }
        
        private class EnumDropDown : AdvancedDropdown 
        {
            private readonly TriValue<Enum> _propertyValue;
            
            public EnumDropDown(TriValue<Enum> propertyValue, AdvancedDropdownState state) : base(state)
            {
                _propertyValue = propertyValue;
                
                minimumSize = new Vector2(0, 175);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(_propertyValue.SmartValue.GetType().Name);

                var values = Enum.GetValues(_propertyValue.SmartValue.GetType());

                foreach (var value in values)
                {
                    root.AddChild(new EnumItem(value as Enum));
                }

                return root;
            }
            
            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (!(item is EnumItem enumItem))
                {
                    return;
                }

                if (enumItem.Value == null)
                {
                    _propertyValue.SetValue(null);
                }
                else
                {
                    _propertyValue.SetValue(enumItem.Value);
                }
            }
            
            private class EnumItem : AdvancedDropdownItem
            {
                public Enum Value { get; }

                public EnumItem(Enum value, Texture2D preview = null) : base(value.ToString())
                {
                    Value = value;
                    icon = preview;
                }
            }
        }
    }

    public class Vector2Drawer : BuiltinDrawerBase<Vector2>
    {
        public override int CompactModeLines => 2;

        protected override Vector2 OnValueGUI(Rect position, GUIContent label, Vector2 value)
        {
            return EditorGUI.Vector2Field(position, label, value);
        }
    }

    public class Vector3Drawer : BuiltinDrawerBase<Vector3>
    {
        public override int CompactModeLines => 2;

        protected override Vector3 OnValueGUI(Rect position, GUIContent label, Vector3 value)
        {
            return EditorGUI.Vector3Field(position, label, value);
        }
    }

    public class Vector4Drawer : BuiltinDrawerBase<Vector4>
    {
        public override int CompactModeLines => 2;

        protected override Vector4 OnValueGUI(Rect position, GUIContent label, Vector4 value)
        {
            return EditorGUI.Vector4Field(position, label, value);
        }
    }

    public class RectDrawer : BuiltinDrawerBase<Rect>
    {
        public override int CompactModeLines => 3;
        public override int WideModeLines => 2;

        protected override Rect OnValueGUI(Rect position, GUIContent label, Rect value)
        {
            return EditorGUI.RectField(position, label, value);
        }
    }

    public class AnimationCurveDrawer : BuiltinDrawerBase<AnimationCurve>
    {
        protected override AnimationCurve OnValueGUI(Rect position, GUIContent label, AnimationCurve value)
        {
            return EditorGUI.CurveField(position, label, value);
        }
    }

    public class BoundsDrawer : BuiltinDrawerBase<Bounds>
    {
        public override int CompactModeLines => 3;
        public override int WideModeLines => 3;

        protected override Bounds OnValueGUI(Rect position, GUIContent label, Bounds value)
        {
            return EditorGUI.BoundsField(position, label, value);
        }
    }

    public class GradientDrawer : BuiltinDrawerBase<Gradient>
    {
        private static readonly GUIContent NullLabel = new GUIContent("Gradient is null");

        protected override Gradient OnValueGUI(Rect position, GUIContent label, Gradient value)
        {
            if (value == null)
            {
                EditorGUI.LabelField(position, label, NullLabel);
                return null;
            }

            return EditorGUI.GradientField(position, label, value);
        }
    }

    public class Vector2IntDrawer : BuiltinDrawerBase<Vector2Int>
    {
        public override int CompactModeLines => 2;

        protected override Vector2Int OnValueGUI(Rect position, GUIContent label, Vector2Int value)
        {
            return EditorGUI.Vector2IntField(position, label, value);
        }
    }

    public class Vector3IntDrawer : BuiltinDrawerBase<Vector3Int>
    {
        public override int CompactModeLines => 2;

        protected override Vector3Int OnValueGUI(Rect position, GUIContent label, Vector3Int value)
        {
            return EditorGUI.Vector3IntField(position, label, value);
        }
    }

    public class RectIntDrawer : BuiltinDrawerBase<RectInt>
    {
        public override int CompactModeLines => 3;
        public override int WideModeLines => 2;

        protected override RectInt OnValueGUI(Rect position, GUIContent label, RectInt value)
        {
            return EditorGUI.RectIntField(position, label, value);
        }
    }

    public class BoundsIntDrawer : BuiltinDrawerBase<BoundsInt>
    {
        public override int CompactModeLines => 3;
        public override int WideModeLines => 3;

        protected override BoundsInt OnValueGUI(Rect position, GUIContent label, BoundsInt value)
        {
            return EditorGUI.BoundsIntField(position, label, value);
        }
    }
}
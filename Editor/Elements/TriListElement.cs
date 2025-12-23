using System;
using System.Collections;
using System.Linq;
using TriInspectorUnityInternalBridge;
using TriInspector.Utilities;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TriInspector.Elements
{
    public class TriListElement : TriElement
    {
        private const float ListExtraWidth = 7f;
        private const float DraggableAreaExtraWidth = 14f;

        private readonly TriProperty _property;
        private readonly ReorderableList _reorderableListGui;
        private readonly bool _alwaysExpanded;
        private readonly bool _alwaysElementsExpanded;
        private readonly Color _oddElementColor;
        private readonly Color _eventElementColor;
        private readonly Color _selectionElementColor;
        private readonly int _maxItemsPerPage;

        private float _lastContentWidth;
        private int _currentPageIndex;

        protected int CurrentPage => _currentPageIndex + 1;
        protected int TotalPages => Mathf.CeilToInt((float)_reorderableListGui.count / _maxItemsPerPage);
        protected ReorderableList ListGui => _reorderableListGui;

        public TriListElement(TriProperty property)
        {
            property.TryGetAttribute(out ListDrawerSettingsAttribute settings);

            _property = property;
            _alwaysExpanded = settings?.AlwaysExpanded ?? false;
            _alwaysElementsExpanded = settings?.AlwaysElementsExpanded ?? false;
            _oddElementColor = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
            _eventElementColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
            _selectionElementColor = EditorGUIUtility.isProSkin ? new Color(0.243f, 0.49f, 0.905f, 0.5f) : new Color(0.243f, 0.49f, 0.905f, 0.3f);
            _maxItemsPerPage = settings?.MaxItemPerPage ?? 50;
            _reorderableListGui = new ReorderableList(null, _property.ArrayElementType)
            {
                draggable = settings?.Draggable ?? true,
                displayAdd = settings == null || !settings.HideAddButton,
                displayRemove = settings == null || !settings.HideRemoveButton,
                drawHeaderCallback = DrawHeaderCallback,
                drawFooterCallback = DrawFooterCallback,
                elementHeightCallback = ElementHeightCallback,
                drawElementCallback = DrawElementCallback,
                drawElementBackgroundCallback = DrawElementBackgroundCallback,
                onAddCallback = AddElementCallback,
                onRemoveCallback = RemoveElementCallback,
                onReorderCallbackWithDetails = ReorderCallback,
            };

            if (!_reorderableListGui.displayAdd && !_reorderableListGui.displayRemove)
            {
                _reorderableListGui.footerHeight = 0f;
            }
        }

        public override bool Update()
        {
            var dirty = false;

            if (_property.TryGetSerializedProperty(out var serializedProperty) && serializedProperty.isArray)
            {
                _reorderableListGui.serializedProperty = serializedProperty;
            }
            else if (_property.Value != null)
            {
                _reorderableListGui.list = (IList)_property.Value;
            }
            else if (_reorderableListGui.list == null)
            {
                _reorderableListGui.list = (IList) (_property.FieldType.IsArray
                    ? Array.CreateInstance(_property.ArrayElementType, 0)
                    : Activator.CreateInstance(_property.FieldType));
            }

            if (_alwaysExpanded && !_property.IsExpanded)
            {
                _property.IsExpanded = true;
            }

            if (_property.IsExpanded)
            {
                dirty |= GenerateChildren();
            }
            else
            {
                dirty |= ClearChildren();
            }

            dirty |= base.Update();

            if (dirty)
            {
                ReorderableListProxy.ClearCacheRecursive(_reorderableListGui);
            }

            return dirty;
        }

        public override float GetHeight(float width)
        {
            if (!_property.IsExpanded)
            {
                return _reorderableListGui.headerHeight;
            }

            _lastContentWidth = width;

            return _reorderableListGui.GetHeight();
        }

        public override void OnGUI(Rect position)
        {
            if (!_property.IsExpanded)
            {
                ReorderableListProxy.DoListHeader(_reorderableListGui, new Rect(position)
                {
                    height = _reorderableListGui.headerHeight,
                });
                return;
            }

            var labelWidthExtra = ListExtraWidth + DraggableAreaExtraWidth;

            using (TriGuiHelper.PushLabelWidth(EditorGUIUtility.labelWidth - labelWidthExtra))
            {
                _reorderableListGui.DoList(position);
            }
        }

        private void AddElementCallback(ReorderableList reorderableList)
        {
            AddElementCallback(reorderableList, null);
        }

        private void AddElementCallback(ReorderableList reorderableList, Object addedReferenceValue)
        {
            if (_property.TryGetSerializedProperty(out _))
            {
                ReorderableListProxy.DoAddButton(reorderableList, addedReferenceValue);
                _property.NotifyValueChanged();
                return;
            }

            var template = CloneValue(_property);

            _property.SetValues(targetIndex =>
            {
                var value = (IList) _property.GetValue(targetIndex);

                if (_property.FieldType.IsArray)
                {
                    var array = Array.CreateInstance(_property.ArrayElementType, template.Length + 1);
                    Array.Copy(template, array, template.Length);

                    if (addedReferenceValue != null)
                    {
                        array.SetValue(addedReferenceValue, array.Length - 1);
                    }

                    value = array;
                }
                else
                {
                    if (value == null)
                    {
                        value = (IList) Activator.CreateInstance(_property.FieldType);
                    }

                    var newElement = addedReferenceValue != null
                        ? addedReferenceValue
                        : CreateDefaultElementValue(_property);

                    value.Add(newElement);
                }

                return value;
            });
        }

        private void RemoveElementCallback(ReorderableList reorderableList)
        {
            if (_property.TryGetSerializedProperty(out _))
            {
                var currentIndex = reorderableList.index;
                var startIndex = _currentPageIndex * _maxItemsPerPage;
                reorderableList.index = currentIndex + startIndex;
                ReorderableListProxy.defaultBehaviours.DoRemoveButton(reorderableList);
                reorderableList.index = Mathf.Clamp(currentIndex, 0, (_reorderableListGui.count - startIndex) - 1);
                _property.NotifyValueChanged();
                
                if (CurrentPage > TotalPages)
                {
                    _currentPageIndex = TotalPages - 1;

                    if (_currentPageIndex < 0)
                    {
                        _currentPageIndex = 0;
                    }

                    ClearChildren();
                    GenerateChildren();
                }

                return;
            }

            var template = CloneValue(_property);
            var ind = reorderableList.index + _currentPageIndex * _maxItemsPerPage;

            _property.SetValues(targetIndex =>
            {
                var value = (IList) _property.GetValue(targetIndex);

                if (_property.FieldType.IsArray)
                {
                    var array = Array.CreateInstance(_property.ArrayElementType, template.Length - 1);
                    Array.Copy(template, 0, array, 0, ind);
                    Array.Copy(template, ind + 1, array, ind, array.Length - ind);
                    value = array;
                }
                else
                {
                    value?.RemoveAt(ind);
                }

                return value;
            });
            
            if (CurrentPage > TotalPages)
            {
                _currentPageIndex = TotalPages - 1;
            }
        }

        private void ReorderCallback(ReorderableList list, int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex) return;

            var property = list.serializedProperty;
            property.MoveArrayElement(newIndex, oldIndex);
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            
            var indexOffset = _currentPageIndex * _maxItemsPerPage;
            var correctedOldIndex = indexOffset + oldIndex;
            var correctedNewIndex = indexOffset + newIndex;
            
            if(correctedNewIndex < indexOffset) correctedNewIndex =  indexOffset;
            if(correctedNewIndex > indexOffset + _maxItemsPerPage - 1) correctedNewIndex = indexOffset + _maxItemsPerPage - 1;
            
            _property.SetValues(targetIndex =>
            {
                var value = (IList) _property.GetValue(targetIndex);

                var element = value[correctedOldIndex];
                for (var index = 0; index < value.Count - 1; ++index)
                {
                    if (index >= correctedOldIndex)
                    {
                        value[index] = value[index + 1];
                    }
                }

                for (var index = value.Count - 1; index > 0; --index)
                {
                    if (index > correctedNewIndex)
                    {
                        value[index] = value[index - 1];
                    }
                }

                value[correctedNewIndex] = element;

                return value;
            });
        }

        private bool GenerateChildren()
        {
            var startIndex = _currentPageIndex * _maxItemsPerPage;
            var count = Mathf.Min(_maxItemsPerPage, _reorderableListGui.count - startIndex);

            if (ChildrenCount == count)
            {
                return false;
            }

            while (ChildrenCount < count)
            {
                var property = _property.ArrayElementProperties[ChildrenCount + startIndex];
                AddChild(CreateItemElement(property));
            }

            while (ChildrenCount > count)
            {
                RemoveChildAt(ChildrenCount - 1);
            }

            return true;
        }

        private bool ClearChildren()
        {
            if (ChildrenCount == 0)
            {
                return false;
            }

            RemoveAllChildren();

            return true;
        }

        protected virtual TriElement CreateItemElement(TriProperty property)
        {
            return new TriPropertyElement(property, new TriPropertyElement.Props
            {
                forceInline = _alwaysElementsExpanded,
            });
        }

        private void DrawHeaderCallback(Rect rect)
        {
            var labelRect = new Rect(rect);
            var arraySizeRect = new Rect(rect)
            {
                xMin = rect.xMax - 100,
            };

            if (_alwaysExpanded)
            {
                EditorGUI.LabelField(labelRect, _property.DisplayNameContent);
            }
            else
            {
                TriEditorGUI.Foldout(labelRect, _property);
            }

            var label = _reorderableListGui.count == 0 ? "Empty" : $"{_reorderableListGui.count} items";
            GUI.Label(arraySizeRect, label, Styles.ItemsCount);

            if (Event.current.type == EventType.DragUpdated && rect.Contains(Event.current.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDrop.objectReferences.All(obj => TryGetDragAndDropObject(obj, out _))
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;

                Event.current.Use();
            }
            else if (Event.current.type == EventType.DragPerform && rect.Contains(Event.current.mousePosition))
            {
                DragAndDrop.AcceptDrag();

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (TryGetDragAndDropObject(obj, out var addedReferenceValue))
                    {
                        AddElementCallback(_reorderableListGui, addedReferenceValue);
                    }
                }

                Event.current.Use();
            }
        }
        
        private void DrawFooterCallback(Rect rect)
        {
            if (TotalPages > 1)
            {
                if (GUI.Button(new Rect(rect.xMin, rect.yMin, 50, _reorderableListGui.footerHeight), "<") &&
                    _currentPageIndex > 0)
                {
                    _currentPageIndex--;
                        
                    ClearChildren();
                    GenerateChildren();
                }

                GUI.Label(new Rect(rect.xMin + 60, rect.yMin, 100, _reorderableListGui.footerHeight),
                    $"Page {CurrentPage} of {TotalPages}");

                if (GUI.Button(new Rect(rect.xMin + 170, rect.yMin, 50, _reorderableListGui.footerHeight),
                        ">") && CurrentPage < TotalPages)
                {
                    _currentPageIndex++;
                        
                    ClearChildren();
                    GenerateChildren();
                }
            }
            
            ReorderableList.defaultBehaviours.DrawFooter(rect, _reorderableListGui);
        }

        private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index > ChildrenCount - 1)
            {
                return;
            }

            if (!_reorderableListGui.draggable)
            {
                rect.xMin += DraggableAreaExtraWidth;
            }

            using (TriPropertyOverrideContext.BeginOverride(ListPropertyOverrideContext.Instance))
            {
                GetChild(index).OnGUI(new Rect(rect));
            }
        }
        
        private void DrawElementBackgroundCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (_reorderableListGui.count > 0)
            {
                if (_reorderableListGui.index != index || !isFocused)
                {
                    EditorGUI.DrawRect(rect, index % 2 == 0 ? _oddElementColor : _eventElementColor);
                }
                else
                {
                    EditorGUI.DrawRect(rect, _selectionElementColor);
                }
            }
        }

        private float ElementHeightCallback(int index)
        {
            if (index > ChildrenCount - 1)
            {
                return 0;
            }

            return GetChild(index).GetHeight(_lastContentWidth);
        }

        private static object CreateDefaultElementValue(TriProperty property)
        {
            var canActivate = property.ArrayElementType.IsValueType ||
                              property.ArrayElementType.GetConstructor(Type.EmptyTypes) != null;

            return canActivate ? Activator.CreateInstance(property.ArrayElementType) : null;
        }

        private static Array CloneValue(TriProperty property)
        {
            var list = (IList) property.Value;
            var template = Array.CreateInstance(property.ArrayElementType, list?.Count ?? 0);
            list?.CopyTo(template, 0);
            return template;
        }

        private bool TryGetDragAndDropObject(Object obj, out Object result)
        {
            if (obj == null)
            {
                result = null;
                return false;
            }

            var elementType = _property.ArrayElementType;
            var objType = obj.GetType();

            if (elementType == objType || elementType.IsAssignableFrom(objType))
            {
                result = obj;
                return true;
            }

            if (obj is GameObject go && typeof(Component).IsAssignableFrom(elementType) &&
                go.TryGetComponent(elementType, out var component))
            {
                result = component;
                return true;
            }

            result = null;
            return false;
        }

        private class ListPropertyOverrideContext : TriPropertyOverrideContext
        {
            public static readonly ListPropertyOverrideContext Instance = new ListPropertyOverrideContext();
            
            private readonly GUIContent _noneLabel = GUIContent.none;

            public override bool TryGetDisplayName(TriProperty property, out GUIContent displayName)
            {
                var showLabels = property.TryGetAttribute(out ListDrawerSettingsAttribute settings) &&
                                 settings.ShowElementLabels;

                if (!showLabels)
                {
                    displayName = _noneLabel;
                    return true;
                }

                displayName = default;
                return false;
            }
        }
 
        private static class Styles
        {
            public static readonly GUIStyle ItemsCount;

            static Styles()
            {
                ItemsCount = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal =
                    {
                        textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.6f, 0.6f, 0.6f)
                            : new Color(0.3f, 0.3f, 0.3f),
                    },
                };
            }
        }
    }
}
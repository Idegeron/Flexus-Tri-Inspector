using System;
using System.Collections;
using System.Collections.Generic;
using TriInspector;
using TriInspector.Drawers;
using TriInspector.Elements;
using TriInspector.Utilities;
using TriInspectorUnityInternalBridge;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

[assembly: RegisterTriValueDrawer(typeof(DictionaryDrawer), TriDrawerOrder.Fallback)]

namespace TriInspector.Drawers
{
    public class DictionaryDrawer : TriValueDrawer<Dictionary<object, object>>
    {
        public override TriElement CreateElement(TriValue<Dictionary<object, object>> propertyValue, TriElement next)
        {
            return new DictionaryElement(propertyValue.Property);
        }

        private class DictionaryElement : TriElement
        {
            private const float DraggableAreaExtraWidth = 14f;
            private const float FooterExtraSpace = 4;

            private readonly TriProperty _triProperty;
            private readonly bool _alwaysExpanded;
            private readonly Type _dictionaryType;
            private readonly Type _arrayElementType;
            private readonly TriProperty _keyTriProperty;
            private readonly TriProperty _valueTriProperty;
            private readonly TriElement _keyTriElement;
            private readonly TriElement _valueTriElement;
            private readonly IList _list;
            private readonly IDictionary _dictionary;
            private readonly ReorderableList _reorderableList;
            private readonly DictionaryTreeView _dictionaryTreeView;

            private object _keyInstance;
            private object _valueInstance;
            private bool _reloadRequired;
            private bool _heightDirty;
            private bool _isExpanded;
            private bool _displayAddBlock;
            private int _arraySize;
            private float _lastContentWidth;

            public DictionaryElement(TriProperty triProperty)
            {
                triProperty.TryGetAttribute<DictionaryDrawerSettingsAttribute>(out var settings);
                
                _triProperty = triProperty;
                _alwaysExpanded = settings?.AlwaysExpanded ?? false;
                _dictionaryType = triProperty.Value != null ? triProperty.Value.GetType() : triProperty.ValueType;
                _arrayElementType = typeof(KeyValuePair<,>).MakeGenericType(_dictionaryType.GetGenericArguments()[0], 
                    _dictionaryType.GetGenericArguments()[1]);
                _keyTriProperty = new TriProperty(triProperty.PropertyTree, null, new TriPropertyDefinition(null, null, 0, "Key", _arrayElementType.GenericTypeArguments[0],
                    (self, index) => _keyInstance,
                    (self, index, value) =>
                    {
                        _keyInstance = value;
                        return _keyInstance;
                    },
                    null, false), null);
                _valueTriProperty = new TriProperty(triProperty.PropertyTree, null, new TriPropertyDefinition(null, null, 1, "Value", _arrayElementType.GenericTypeArguments[1],
                    (self, index) => _valueInstance,
                    (self, index, value) =>
                    {
                        _valueInstance = value;
                        return _valueInstance;
                    },
                    null, false), null);
                _keyTriElement = new TriPropertyElement(_keyTriProperty);
                _valueTriElement = new TriPropertyElement(_valueTriProperty);
                _list = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(_arrayElementType));
                _dictionary = (IDictionary) triProperty.Value;
                
                _reorderableList = new ReorderableList(null, _arrayElementType)
                {
                    list = _list,
                    draggable = false,
                    displayAdd = true,
                    displayRemove = true,
                    drawHeaderCallback = DrawHeaderCallback,
                    elementHeightCallback = ElementHeightCallback,
                    drawElementCallback = DrawElementCallback,
                    onAddCallback = AddElementCallback,
                    onRemoveCallback = RemoveElementCallback,
                    onReorderCallbackWithDetails = ReorderCallback,
                };
                
                _dictionaryTreeView = new DictionaryTreeView(triProperty, this, _list, _reorderableList)
                {
                    SelectionChangedCallback = SelectionChangedCallback,
                };
                
                _reloadRequired = true;

                _keyTriElement.AttachInternal();
                _valueTriElement.AttachInternal();

                ReloadList();
            }

            public override bool Update()
            {
                var dirty = false;

                if (_alwaysExpanded && !_triProperty.IsExpanded)
                {
                    _triProperty.IsExpanded = true;
                }
                
                if (_triProperty.IsExpanded)
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
                    ReorderableListProxy.ClearCacheRecursive(_reorderableList);
                }

                dirty |= ReloadIfRequired();

                if (dirty)
                {
                    _heightDirty = true;
                    _dictionaryTreeView.multiColumnHeader.ResizeToFit();
                }

                _keyTriElement.Update();
                _valueTriElement.Update();

                return dirty;
            }

            public override float GetHeight(float width)
            {
                _dictionaryTreeView.Width = width;

                if (_heightDirty)
                {
                    _heightDirty = false;
                    _dictionaryTreeView.RefreshHeight();
                }

                var height = 0f;
                height += _reorderableList.headerHeight;

                if (_triProperty.IsExpanded)
                {
                    height += _dictionaryTreeView.totalHeight;
                    height += _reorderableList.footerHeight;
                    height += FooterExtraSpace;
                }
                
                if (_displayAddBlock)
                {
                    height += _keyTriElement.GetHeight(width) + _valueTriElement.GetHeight(width) + EditorGUIUtility.standardVerticalSpacing * 6;
                }

                return height;
            }

            public override void OnGUI(Rect position)
            {
                var headerRect = new Rect(position)
                {
                    height = _reorderableList.headerHeight,
                };
                var elementsRect = new Rect(position)
                {
                    yMin = headerRect.yMax,
                    height = _dictionaryTreeView.totalHeight + FooterExtraSpace,
                };
                var elementsContentRect = new Rect(elementsRect)
                {
                    xMin = elementsRect.xMin + 1,
                    xMax = elementsRect.xMax - 1,
                    yMax = elementsRect.yMax - FooterExtraSpace,
                };
                var footerRect = new Rect(position)
                {
                    yMin = elementsRect.yMax,
                };

                if (!_triProperty.IsExpanded)
                {
                    ReorderableListProxy.DoListHeader(_reorderableList, headerRect);
                    return;
                }

                if (Event.current.isMouse && Event.current.type == EventType.MouseDrag)
                {
                    _heightDirty = true;
                    _dictionaryTreeView.multiColumnHeader.ResizeToFit();
                }

                if (Event.current.type == EventType.Repaint)
                {
                    ReorderableListProxy.defaultBehaviours.boxBackground.Draw(elementsRect,
                        false, false, false, false);
                }

                using (TriPropertyOverrideContext.BeginOverride(new DictionaryElementPropertyOverrideContext()))
                {
                    ReorderableListProxy.DoListHeader(_reorderableList, headerRect);
                }

                EditorGUI.BeginChangeCheck();

                _dictionaryTreeView.OnGUI(elementsContentRect);

                if (EditorGUI.EndChangeCheck())
                {
                    _heightDirty = true;
                    _triProperty.PropertyTree.RequestRepaint();
                }

                ReorderableListProxy.defaultBehaviours.DrawFooter(footerRect, _reorderableList);

                if (_displayAddBlock)
                {
                    DisplayAddBlock(new Rect(footerRect)
                    {
                        yMin = footerRect.yMin - EditorGUIUtility.standardVerticalSpacing,
                        height = _keyTriElement.GetHeight(footerRect.width) 
                                 + _valueTriElement.GetHeight(footerRect.width) 
                                 + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 6
                    });
                }
            }

            private void DrawHeaderCallback(Rect rect)
            {
                var arraySizeRect = new Rect(rect)
                {
                    xMin = rect.xMax - 100,
                };

                var content = _triProperty.DisplayNameContent;
                    
                if (_alwaysExpanded)
                {
                    EditorGUI.LabelField(rect, _triProperty.DisplayNameContent);
                }
                else
                {
                    _triProperty.IsExpanded = EditorGUI.Foldout(rect, _triProperty.IsExpanded, content, true);
                }

                var label = _reorderableList.count == 0 ? "Empty" : $"{_reorderableList.count} items";
                
                GUI.Label(arraySizeRect, label, new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal =
                    {
                        textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.6f, 0.6f, 0.6f)
                            : new Color(0.3f, 0.3f, 0.3f),
                    },
                });
            }
            
            private float ElementHeightCallback(int index)
            {
                if (index >= ChildrenCount)
                {
                    return EditorGUIUtility.singleLineHeight;
                }

                return GetChild(index).GetHeight(_lastContentWidth);
            }

            private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
            {
                if (index >= ChildrenCount)
                {
                    return;
                }

                if (!_reorderableList.draggable)
                {
                    rect.xMin += DraggableAreaExtraWidth;
                }

                using (TriPropertyOverrideContext.BeginOverride(new DictionaryElementPropertyOverrideContext()))
                {
                    GetChild(index).OnGUI(rect);
                }
            }
            
            private void SelectionChangedCallback(int index)
            {
                _reorderableList.index = index;
            }
           
            private void AddElementCallback(ReorderableList reorderableList)
            {
                _displayAddBlock = !_displayAddBlock;
                
                _triProperty.PropertyTree.RequestRepaint();
            }
            
            private void RemoveElementCallback(ReorderableList reorderableList)
            {
                var ind = reorderableList.index;

                _dictionary.Remove(_arrayElementType.GetProperty("Key").GetValue(_list[ind]));
                
                _list.RemoveAt(ind);
                
                _triProperty.PropertyTree.RequestRepaint();
            }
            
            private void ReorderCallback(ReorderableList reorderableList, int oldIndex, int newIndex)
            {
                var mainValue = _triProperty.Value;

                _triProperty.SetValues(targetIndex =>
                {
                    var value = (IList) _triProperty.GetValue(targetIndex);

                    if (value == mainValue)
                    {
                        return value;
                    }

                    var element = value[oldIndex];
                    for (var index = 0; index < value.Count - 1; ++index)
                    {
                        if (index >= oldIndex)
                        {
                            value[index] = value[index + 1];
                        }
                    }

                    for (var index = value.Count - 1; index > 0; --index)
                    {
                        if (index > newIndex)
                        {
                            value[index] = value[index - 1];
                        }
                    }

                    value[newIndex] = element;

                    return value;
                });
            }

            private TriProperty CreteTriProperty(int childIndex)
            {
                var triProperty = new TriProperty(_triProperty.PropertyTree, null, new TriPropertyDefinition(null, null,
                    0, string.Empty, _arrayElementType,
                    (self, index) => _list[childIndex],
                    (self, index, value) =>
                    {
                        var key = _arrayElementType.GetProperty("Key").GetValue(value);
                        var newValue = _arrayElementType.GetProperty("Value").GetValue(value);

                        _dictionary[key] = newValue;
                     
                        _list[childIndex] = value;
                        
                        return value;
                    },
                    null, false), null);
                
                return triProperty;
            }
            
            private TriElement CreateItemElement(TriProperty triProperty)
            {
                return new DictionaryRowElement(triProperty);
            }
            
            private bool GenerateChildren()
            {
                var count = _list.Count;

                if (ChildrenCount == count)
                {
                    return false;
                }

                while (ChildrenCount < count)
                {
                    AddChild(CreateItemElement(CreteTriProperty(ChildrenCount)));
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
            
            private bool ReloadIfRequired()
            {
                if (!_reloadRequired &&
                    _triProperty.IsExpanded == _isExpanded &&
                    _dictionary.Count == _arraySize)
                {
                    return false;
                }

                if (_dictionary.Count != _arraySize)
                {
                    ReloadList();
                    
                    GenerateChildren();
                }
                
                _reloadRequired = false;
                _isExpanded = _triProperty.IsExpanded;
                _arraySize = _dictionary.Count;

                _dictionaryTreeView.Reload();
                
                return true;
            }

            private void ReloadList()
            {
                _list.Clear();
                
                foreach (DictionaryEntry entry in _dictionary)
                {
                    _list.Add(Activator.CreateInstance(_arrayElementType, entry.Key, entry.Value));
                }
            }
            
            private void DisplayAddBlock(Rect position)
            {
                var keyRect = new Rect(position.xMin + EditorGUIUtility.standardVerticalSpacing, position.yMin + EditorGUIUtility.standardVerticalSpacing * 2, position.width - EditorGUIUtility.standardVerticalSpacing * 2, _keyTriElement.GetHeight(position.width));
                var valueRect = new Rect(keyRect.xMin, keyRect.yMax + EditorGUIUtility.standardVerticalSpacing, keyRect.width,_valueTriElement.GetHeight(position.width));
                var buttonDoneRect = new Rect(valueRect.xMin, valueRect.yMax + EditorGUIUtility.standardVerticalSpacing, valueRect.width/2, EditorGUIUtility.singleLineHeight);
                var buttonCancelRect = new Rect(buttonDoneRect.xMin + buttonDoneRect.width, valueRect.yMax + EditorGUIUtility.standardVerticalSpacing, valueRect.width/2, EditorGUIUtility.singleLineHeight);
                
                TriEditorGUI.DrawBox(position, TriEditorStyles.Box);

                _keyTriElement.OnGUI(keyRect);
                _valueTriElement.OnGUI(valueRect);

                GUI.enabled = _keyInstance != null && !_dictionary.Contains(_keyInstance);
                
                if (GUI.Button(buttonDoneRect, "Done"))
                {
                    _dictionary.Add(_keyInstance, _valueInstance);
                    
                    _list.Add(Activator.CreateInstance(_arrayElementType, _keyInstance, _valueInstance));
                    
                    _keyInstance = default;
                    _valueInstance = default;
                    
                    _triProperty.PropertyTree.RequestRepaint();
                }

                GUI.enabled = true;
                
                if (GUI.Button(buttonCancelRect, "Cancel"))
                {
                    _displayAddBlock = false;
                }
            }
        }

        private class DictionaryRowElement : TriPropertyCollectionBaseElement
        {
            public List<KeyValuePair<TriElement, GUIContent>> TriElements { get; }

            public DictionaryRowElement(TriProperty triProperty)
            {
                DeclareGroups(triProperty.ValueType);

                TriElements = new List<KeyValuePair<TriElement, GUIContent>>();

                if (triProperty.PropertyType == TriPropertyType.Generic)
                {
                    foreach (var childProperty in triProperty.ChildrenProperties)
                    {
                        var oldChildrenCount = ChildrenCount;
                        var props = new TriPropertyElement.Props
                        {
                            forceInline = true,
                        };
                        
                        AddProperty(childProperty, props, out var group);

                        if (oldChildrenCount != ChildrenCount)
                        {
                            var element = GetChild(ChildrenCount - 1);
                            var headerContent = new GUIContent(group ?? childProperty.DisplayName);

                            TriElements.Add(new KeyValuePair<TriElement, GUIContent>(element, headerContent));
                        }
                    }
                }
                else
                {
                    var element = new TriPropertyElement(triProperty, new TriPropertyElement.Props
                    {
                        forceInline = true,
                    });
                    var headerContent = new GUIContent("Element");

                    AddChild(element);
                    
                    TriElements.Add(new KeyValuePair<TriElement, GUIContent>(element, headerContent));
                }
            }
        }
        
        [Serializable]
        private class DictionaryTreeView : TreeView
        {
            private readonly TriProperty _triProperty;
            private readonly TriElement _triElement;
            private readonly int _maxItemsPerPage = 50;
            private readonly IList _list;
            private readonly ReorderableList _reorderableList;
            private readonly DictionaryTreeItemPropertyOverrideContext _dictionaryTreeItemPropertyOverrideContext;
            private readonly DictionaryTreeItemPropertyOverrideAvailability _dictionaryTreeItemPropertyOverrideAvailability;

            private bool _wasRendered;
            private int _currentPage;

            private int TotalPages => Mathf.CeilToInt((float)_list.Count / _maxItemsPerPage);
            
            public Action<int> SelectionChangedCallback;

            public DictionaryTreeView(TriProperty triProperty, TriElement triElement, IList list, ReorderableList reorderableList)
                : base(new TreeViewState(), new DictionaryColumnHeader())
            {
                triProperty.TryGetAttribute<DictionaryDrawerSettingsAttribute>(out var settings);
                
                _triProperty = triProperty;
                _triElement = triElement;
                _maxItemsPerPage = settings?.MaxItemPerPage ?? 50;
                _list = list;
                _reorderableList = reorderableList;
                _dictionaryTreeItemPropertyOverrideContext = new DictionaryTreeItemPropertyOverrideContext();
                _dictionaryTreeItemPropertyOverrideAvailability = new DictionaryTreeItemPropertyOverrideAvailability();

                showAlternatingRowBackgrounds = true;
                showBorder = false;
                useScrollView = false;

                multiColumnHeader.ResizeToFit();
                multiColumnHeader.visibleColumnsChanged += header => header.ResizeToFit();
            }

            public float Width { get; set; }

            public void RefreshHeight()
            {
                RefreshCustomRowHeights();
            }
            
            public override void OnGUI(Rect rect)
            {
                base.OnGUI(rect);
                
                if (TotalPages > 1)
                {
                    var paginationRect = new Rect(rect.xMin, rect.yMax + 5, rect.width, EditorGUIUtility.singleLineHeight);

                    if (GUI.Button(new Rect(paginationRect.xMin, paginationRect.yMin, 50, paginationRect.height), "<") && _currentPage > 0)
                    {
                        _currentPage--;
                        Reload();  
                    }

                    GUI.Label(new Rect(paginationRect.xMin + 60, paginationRect.yMin, 100, paginationRect.height), $"Page {_currentPage + 1} of {TotalPages}");

                    if (GUI.Button(new Rect(paginationRect.xMin + 170, paginationRect.yMin, 50, paginationRect.height), ">") && _currentPage < TotalPages - 1)
                    {
                        _currentPage++;
                        Reload(); 
                    }
                }
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                base.SelectionChanged(selectedIds);

                if (SelectionChangedCallback != null && selectedIds.Count == 1)
                {
                    SelectionChangedCallback.Invoke(selectedIds[0]);
                }
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(0, -1, string.Empty);
                var columns = new List<MultiColumnHeaderState.Column>
                {
                    new MultiColumnHeaderState.Column
                    {
                        width = 16, autoResize = false, canSort = false, allowToggleVisibility = false,
                    },
                };

                if (_triProperty.IsExpanded)
                {
                    var startIndex = _currentPage * _maxItemsPerPage;
                    var endIndex = Mathf.Min(startIndex + _maxItemsPerPage, _list.Count);

                    for (var index = startIndex; index < endIndex; index++)
                    {
                        root.AddChild(new TreeViewItem(index, 0));

                        if (index == startIndex)
                        {
                            foreach (var kvp in ((DictionaryRowElement) (_triElement.GetChild(index - startIndex))).TriElements)
                            {
                                columns.Add(new MultiColumnHeaderState.Column
                                {
                                    headerContent = kvp.Value,
                                    headerTextAlignment = TextAlignment.Center,
                                    autoResize = true,
                                    canSort = false,
                                });
                            }
                        }
                    }
                }

                if (root.children == null)
                {
                    root.AddChild(new DictionaryTreeEmptyItem());
                }

                if (multiColumnHeader.state == null ||
                    multiColumnHeader.state.columns.Length == 1)
                {
                    multiColumnHeader.state = new MultiColumnHeaderState(columns.ToArray());
                }

                return root;
            }

            protected override float GetCustomRowHeight(int row, TreeViewItem item)
            {
                if (item is DictionaryTreeEmptyItem)
                {
                    return EditorGUIUtility.singleLineHeight;
                }

                var height = 0f;
                var startIndex = _currentPage * _maxItemsPerPage;
                var pageIndex = startIndex + row;
                var rowElement = (DictionaryRowElement) _triElement.GetChild(pageIndex);

                foreach (var visibleColumnIndex in multiColumnHeader.state.visibleColumns)
                {
                    var cellWidth = _wasRendered
                        ? multiColumnHeader.GetColumnRect(visibleColumnIndex).width
                        : Width / Mathf.Max(1, multiColumnHeader.state.visibleColumns.Length);

                    var cellHeight = visibleColumnIndex == 0
                        ? EditorGUIUtility.singleLineHeight
                        : rowElement.TriElements[visibleColumnIndex - 1].Key.GetHeight(cellWidth);

                    height = Math.Max(height, cellHeight);
                }

                return height + EditorGUIUtility.standardVerticalSpacing * 2;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                if (args.item is DictionaryTreeEmptyItem)
                {
                    base.RowGUI(args);
                    return;
                }
                
                var startIndex = _currentPage * _maxItemsPerPage;
                var pageIndex = startIndex + args.row;
                var rowElement = (DictionaryRowElement) _triElement.GetChild(pageIndex);
                
                for (var i = 0; i < multiColumnHeader.state.visibleColumns.Length; i++)
                {
                    var visibleColumnIndex = multiColumnHeader.state.visibleColumns[i];
                    var rowIndex = args.row;

                    var cellRect = args.GetCellRect(i);
                    cellRect.yMin += EditorGUIUtility.standardVerticalSpacing;

                    if (visibleColumnIndex == 0)
                    {
                        ReorderableListProxy.defaultBehaviours.DrawElementDraggingHandle(cellRect, rowIndex,
                            _reorderableList.index == rowIndex, _reorderableList.index == rowIndex, _reorderableList.draggable);
                        continue;
                    }

                    var cellElement = rowElement.TriElements[visibleColumnIndex - 1].Key;
                    cellRect.height = cellElement.GetHeight(cellRect.width);

                    using (TriGuiHelper.PushLabelWidth(EditorGUIUtility.labelWidth / rowElement.ChildrenCount))
                    using (TriPropertyOverrideContext.BeginOverride(_dictionaryTreeItemPropertyOverrideContext))
                    using (TriPropertyOverrideAvailability.BeginOverride(_dictionaryTreeItemPropertyOverrideAvailability))
                    {
                        cellElement.OnGUI(cellRect);
                    }
                }

                _wasRendered = true;
            }
        }
        
        [Serializable]
        private class DictionaryColumnHeader : MultiColumnHeader
        {
            public DictionaryColumnHeader() : base(null)
            {
                canSort = false;
                height = DefaultGUI.minimumHeight;
            }
        }

        [Serializable]
        private class DictionaryTreeEmptyItem : TreeViewItem
        {
            public DictionaryTreeEmptyItem() : base(0, 0, "Table is Empty")
            {
            }
        }

        private class DictionaryElementPropertyOverrideContext : TriPropertyOverrideContext
        {
            public override bool TryGetDisplayName(TriProperty property, out GUIContent displayName)
            {
                var showLabels = property.TryGetAttribute(out DictionaryDrawerSettingsAttribute settings) &&
                                 settings.ShowElementLabels;

                if (!showLabels)
                {
                    displayName = GUIContent.none;
                    return true;
                }

                displayName = default;
                return false;
            }
        }
        
        private class DictionaryTreeItemPropertyOverrideContext : TriPropertyOverrideContext
        {
            public override bool TryGetDisplayName(TriProperty property, out GUIContent displayName)
            {
                displayName = GUIContent.none;
                return true;
            }
        }
        
        private class DictionaryTreeItemPropertyOverrideAvailability : TriPropertyOverrideAvailability
        {
            public override bool TryIsEnable(TriProperty property, out bool isEnable)
            {
                isEnable = !property.RawName.Equals("Key");

                return !isEnable;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Spine.UI.ContextualSettings;
using UnityEngine;
using Verse;

namespace Spine.UI.SettingsFramework
{
    /// <summary>
    /// Draws a scrollable list of hierarchical settings with search and view toggles.
    /// </summary>
    public class SettingsListDrawer
    {
        private const float ResetIconSlotWidth = 26f;
        private const float ResetButtonSize = 20f;
        private const float ToolbarGap = 8f;
        private const float FocusHighlightSeconds = 1.45f;
        private const float SearchResultDoubleClickMaxSeconds = 0.25f;
        private const float SearchResultDoubleClickMoveTolerance = 5f;

        private readonly SettingsHierarchy _hierarchy;
        private Vector2 _scrollPosition;
        private string _searchQuery = string.Empty;
        private readonly QuickSearchWidget _searchWidget = new QuickSearchWidget();
        private SettingsFilterDefinition _activeFilter;
        private string _pendingFocusSettingId;
        private string _highlightedSettingId;
        private float _highlightStartedAt;
        private SettingsViewMode? _pendingContextViewMode;
        private string _lastSearchClickSettingId;
        private float _lastSearchClickTime = -1f;
        private Vector2 _lastSearchClickPosition;
        private readonly HashSet<string> _forceVisibleDisabledAncestorIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the current scroll position. Used for preserving scroll state across drawer recreations.
        /// </summary>
        public Vector2 ScrollPosition
        {
            get => _scrollPosition;
            set => _scrollPosition = value;
        }

        /// <summary>
        /// Pixels of indentation per hierarchy level.
        /// </summary>
        public float IndentPerLevel { get; set; } = 20f;

        /// <summary>
        /// Height of each row.
        /// </summary>
        public float RowHeight { get; set; } = 32f;

        /// <summary>
        /// Callback to translate labels.
        /// </summary>
        public Func<SettingDefinition, string> GetLabel { get; set; }

        /// <summary>
        /// Callback to translate tooltips.
        /// </summary>
        public Func<SettingDefinition, string> GetTooltip { get; set; }

        /// <summary>
        /// Display text for the Simple view toggle.
        /// </summary>
        public string SimpleLabel { get; set; } = "Simple";

        /// <summary>
        /// Display text for the Advanced view toggle.
        /// </summary>
        public string AdvancedLabel { get; set; } = "Advanced";

        /// <summary>
        /// Display text when no settings match.
        /// </summary>
        public string NoResultsLabel { get; set; } = "No results";

        /// <summary>
        /// Label for the color edit button.
        /// </summary>
        public string EditColorLabel { get; set; } = "Edit";

        /// <summary>
        /// Additional guidance appended to color-setting tooltips when live preview is available.
        /// </summary>
        public string ColorPreviewTooltip { get; set; }

        /// <summary>
        /// Optional host-owned target for semantic color previews.
        /// </summary>
        public ISettingColorPreviewSink ColorPreviewSink { get; set; }

        /// <summary>
        /// When true, changed field-backed settings show a small per-row reset button.
        /// </summary>
        public bool ShowResetIcons { get; set; } = true;

        /// <summary>
        /// Tooltip for per-setting reset buttons.
        /// </summary>
        public string ResetToDefaultLabel { get; set; } = "Reset to default";

        /// <summary>
        /// Pulse color used when a context jump or search double-click focuses a setting row.
        /// </summary>
        public Color FocusHighlightColor { get; set; } = new Color(1f, 0.78f, 0.18f, 1f);

        /// <summary>
        /// Optional filters shown by the toolbar filter button.
        /// </summary>
        public IReadOnlyList<SettingsFilterDefinition> Filters { get; set; } = Array.Empty<SettingsFilterDefinition>();

        /// <summary>
        /// Toolbar label shown when no filter is active.
        /// </summary>
        public string FilterLabel { get; set; } = "Filter";

        /// <summary>
        /// Menu label that clears the active filter.
        /// </summary>
        public string AllSettingsFilterLabel { get; set; } = "All settings";

        /// <summary>
        /// Optional callback invoked when a row's tooltip is actually hovered.
        /// </summary>
        public Action<SettingDefinition, object> OnSettingTooltipViewed { get; set; }

        /// <summary>
        /// Creates a new drawer for a hierarchy.
        /// </summary>
        public SettingsListDrawer(SettingsHierarchy hierarchy)
        {
            _hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        }

        public void ApplyContextFilter(SettingsFilterDefinition filter, string targetSettingId)
        {
            if (filter == null)
            {
                return;
            }

            _activeFilter = filter;
            _scrollPosition = Vector2.zero;
            FocusSetting(targetSettingId);
            _pendingFocusSettingId = targetSettingId;
            ClearSearch();
        }

        internal void PrepareContextNavigation(
            ContextualSettingsTarget requestedTarget,
            object settingsObject)
        {
            ContextualNavigationPlan plan = ContextualNavigationResolver.Resolve(
                requestedTarget,
                id =>
                {
                    SettingDefinition definition = _hierarchy.GetById(id);
                    bool available = definition != null &&
                        (definition.VisibleWhen == null ||
                            definition.VisibleWhen(settingsObject)) &&
                        (definition.ShowInSimpleView ||
                            definition.ShowInAdvancedView);
                    return new ContextualNavigationCandidate(
                        definition?.Id,
                        available,
                        definition?.ShowInSimpleView == true);
                });
            if (plan.IsRoot)
            {
                ClearActiveFilter();
                ClearSearch();
                _scrollPosition = Vector2.zero;
                _pendingFocusSettingId = null;
                _highlightedSettingId = null;
                _pendingContextViewMode = null;
                return;
            }

            ClearActiveFilter();
            ClearSearch();
            _pendingFocusSettingId = plan.TargetId;
            FocusSetting(plan.TargetId);
            _pendingContextViewMode =
                !SettingsPresentationPolicy.ShowViewModes(
                    _hierarchy.SettingCount,
                    _hierarchy.AdvancedOnlySettingCount)
                    ? SettingsViewMode.All
                    : plan.UseSimpleView
                        ? SettingsViewMode.Simple
                        : SettingsViewMode.Advanced;
        }

        /// <summary>
        /// Draws the full UI for the settings list including search and view toggle.
        /// </summary>
        public void Draw(
            Rect rect,
            object settingsObject,
            ref SettingsViewMode viewMode,
            Action onSettingsChanged = null)
        {
            if (settingsObject == null)
            {
                return;
            }

            if (_pendingContextViewMode.HasValue)
            {
                viewMode = _pendingContextViewMode.Value;
                _pendingContextViewMode = null;
            }

            bool drawHeader = ShouldDrawHeader();
            float listStartY = rect.y;
            if (drawHeader)
            {
                const float headerHeight = 30f;
                Rect headerRect = new Rect(
                    rect.x,
                    rect.y,
                    rect.width,
                    headerHeight);
                DrawHeader(headerRect, ref viewMode);
                listStartY = headerRect.yMax + 10f;
            }
            else
            {
                viewMode = SettingsViewMode.All;
                ClearActiveFilter();
                ClearSearch();
            }

            Rect listRect = new Rect(rect.x, listStartY, rect.width, rect.height - (listStartY - rect.y));
            DrawSettingsList(listRect, settingsObject, ref viewMode, onSettingsChanged);
        }

        private void DrawHeader(Rect rect, ref SettingsViewMode viewMode)
        {
            bool showSearch = SettingsPresentationPolicy.ShowSearch(
                _hierarchy.SettingCount);
            bool showViewModes = SettingsPresentationPolicy.ShowViewModes(
                _hierarchy.SettingCount,
                _hierarchy.AdvancedOnlySettingCount);
            bool showFilters = SettingsPresentationPolicy.ShowFilters(
                _hierarchy.SettingCount);
            bool hasFilters =
                showFilters &&
                Filters != null &&
                Filters.Count > 0;
            if (!showFilters)
            {
                ClearActiveFilter();
            }

            float toggleWidth = showViewModes ? 200f : 0f;
            float filterWidth = hasFilters ? 150f : 0f;
            int visibleControlCount =
                (showSearch ? 1 : 0) +
                (hasFilters ? 1 : 0) +
                (showViewModes ? 1 : 0);
            float gaps = Mathf.Max(0, visibleControlCount - 1) * ToolbarGap;
            float searchWidth = showSearch
                ? Mathf.Max(120f, rect.width - toggleWidth - filterWidth - gaps)
                : 0f;
            Rect searchRect = new Rect(rect.x, rect.y, searchWidth, rect.height);
            float filterX = showSearch
                ? searchRect.xMax + ToolbarGap
                : rect.x;
            Rect filterRect = new Rect(filterX, rect.y, filterWidth, rect.height);
            Rect toggleRect = new Rect(rect.xMax - 200f, rect.y, 200f, rect.height);

            if (showSearch)
            {
                _searchWidget.OnGUI(searchRect, () => { });
                _searchQuery = _searchWidget.filter.Text ?? string.Empty;
            }
            else
            {
                ClearSearch();
            }

            if (hasFilters)
            {
                DrawFilterButton(filterRect);
            }

            if (showViewModes)
            {
                DrawViewToggle(toggleRect, ref viewMode);
            }
            else
            {
                viewMode = SettingsViewMode.All;
            }
        }

        private bool ShouldDrawHeader()
        {
            return SettingsPresentationPolicy.ShowSearch(
                    _hierarchy.SettingCount) ||
                SettingsPresentationPolicy.ShowFilters(
                    _hierarchy.SettingCount) &&
                Filters != null &&
                Filters.Count > 0 ||
                SettingsPresentationPolicy.ShowViewModes(
                    _hierarchy.SettingCount,
                    _hierarchy.AdvancedOnlySettingCount);
        }

        private void DrawFilterButton(Rect rect)
        {
            string label = _activeFilter != null ? _activeFilter.Label : FilterLabel;
            Event evt = Event.current;
            if (_activeFilter != null &&
                evt != null &&
                evt.type == EventType.MouseDown &&
                evt.button == 1 &&
                rect.Contains(evt.mousePosition))
            {
                ClearActiveFilter();
                evt.Use();
                return;
            }

            if (!Widgets.ButtonText(rect, label))
            {
                if (_activeFilter != null && !string.IsNullOrEmpty(_activeFilter.Tooltip))
                {
                    TooltipHandler.TipRegion(rect, _activeFilter.Tooltip);
                }

                return;
            }

            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(AllSettingsFilterLabel, ClearActiveFilter)
            };

            if (HasFilterCategories())
            {
                AddFilterCategoryOptions(options);
                Find.WindowStack.Add(new FloatMenu(options));
                return;
            }

            foreach (var filter in Filters)
            {
                if (filter == null)
                {
                    continue;
                }

                var localFilter = filter;
                options.Add(new FloatMenuOption(localFilter.Label ?? localFilter.Id, () => ApplyFilter(localFilter)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private bool HasFilterCategories()
        {
            foreach (var filter in Filters)
            {
                if (!string.IsNullOrEmpty(filter?.Category) || !string.IsNullOrEmpty(filter?.CategoryLabel))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddFilterCategoryOptions(List<FloatMenuOption> options)
        {
            var categories = new List<FilterCategory>();
            foreach (var filter in Filters)
            {
                if (filter == null)
                {
                    continue;
                }

                string id = string.IsNullOrEmpty(filter.Category) ? "other" : filter.Category;
                string label = string.IsNullOrEmpty(filter.CategoryLabel) ? id : filter.CategoryLabel;
                FilterCategory category = categories.Find(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
                if (category == null)
                {
                    category = new FilterCategory(id, label);
                    categories.Add(category);
                }

                category.Filters.Add(filter);
            }

            foreach (var category in categories)
            {
                var localCategory = category;
                options.Add(new FloatMenuOption(localCategory.Label, () => OpenFilterCategoryMenu(localCategory)));
            }
        }

        private void OpenFilterCategoryMenu(FilterCategory category)
        {
            if (category == null)
            {
                return;
            }

            var options = new List<FloatMenuOption>();

            foreach (var filter in category.Filters)
            {
                if (filter == null)
                {
                    continue;
                }

                var localFilter = filter;
                options.Add(new FloatMenuOption(localFilter.Label ?? localFilter.Id, () => ApplyFilter(localFilter)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private sealed class FilterCategory
        {
            internal readonly string Id;
            internal readonly string Label;
            internal readonly List<SettingsFilterDefinition> Filters = new List<SettingsFilterDefinition>();

            internal FilterCategory(string id, string label)
            {
                Id = id;
                Label = label;
            }
        }

        /// <summary>
        /// Draws the simple/advanced view toggle buttons.
        /// </summary>
        private void DrawViewToggle(Rect rect, ref SettingsViewMode viewMode)
        {
            Rect simpleRect = rect.LeftHalf().ContractedBy(2f);
            Rect advancedRect = rect.RightHalf().ContractedBy(2f);

            bool isSimple = viewMode == SettingsViewMode.Simple;

            GUI.color = isSimple ? Color.white : Color.gray;
            if (Widgets.ButtonText(simpleRect, SimpleLabel))
            {
                viewMode = SettingsViewMode.Simple;
            }

            GUI.color = !isSimple ? Color.white : Color.gray;
            if (Widgets.ButtonText(advancedRect, AdvancedLabel))
            {
                viewMode = SettingsViewMode.Advanced;
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// Draws the scrollable list of settings.
        /// </summary>
        private void DrawSettingsList(
            Rect rect,
            object settingsObject,
            ref SettingsViewMode viewMode,
            Action onSettingsChanged)
        {
            bool isSearching = !string.IsNullOrWhiteSpace(_searchQuery);
            if (!string.IsNullOrEmpty(_pendingFocusSettingId))
            {
                RevealDisabledAncestorChain(_pendingFocusSettingId);
            }

            var visibleSettings = BuildVisibleSettings(settingsObject, viewMode, isSearching);

            if (visibleSettings.Count == 0)
            {
                DrawEmptyState(rect, settingsObject, ref viewMode);
                return;
            }

            if (!string.IsNullOrEmpty(_pendingFocusSettingId))
            {
                CenterOnSettingId(_pendingFocusSettingId, visibleSettings, settingsObject, rect.height);
                _pendingFocusSettingId = null;
            }

            float clearFilterRowHeight = _activeFilter != null ? RowHeight + 8f : 0f;
            float viewHeight = MeasureTotalHeight(visibleSettings, settingsObject) + clearFilterRowHeight;
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight);

            Widgets.BeginScrollView(rect, ref _scrollPosition, viewRect);

            float curY = 0f;
            foreach (var def in visibleSettings)
            {
                int depth = _hierarchy.GetDepth(def);
                bool disabledByAncestor = _hierarchy.IsDisabledByAncestor(def, settingsObject);
                bool allowFocusedDisabledInteraction =
                    disabledByAncestor && IsFocusedForcedVisibleSetting(def);

                float rowHeight = MeasureRowHeight(def, settingsObject);
                Rect rowRect = new Rect(0f, curY, viewRect.width, rowHeight);
                if (isSearching)
                {
                    TryHandleSearchResultDoubleClick(rowRect, def, settingsObject, viewMode, rect.height);
                }

                DrawFocusedSettingHighlight(rowRect, def);
                DrawSettingRow(
                    rowRect,
                    def,
                    settingsObject,
                    disabledByAncestor && !allowFocusedDisabledInteraction,
                    depth,
                    onSettingsChanged);
                curY += rowHeight;
            }

            if (_activeFilter != null)
            {
                DrawClearFilterRow(new Rect(0f, curY + 4f, viewRect.width, RowHeight));
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws a single setting row with indentation and disabled state.
        /// </summary>
        private void DrawSettingRow(
            Rect rect,
            SettingDefinition def,
            object settingsObject,
            bool isDisabledByParent,
            int depth,
            Action onSettingsChanged)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Rect controlRow = rect;

            float indent = depth * IndentPerLevel;
            Rect contentRect = new Rect(controlRow.x + indent, controlRow.y, controlRow.width - indent, controlRow.height);

            bool disabled = isDisabledByParent;
            string label = GetLabel?.Invoke(def) ?? def.Label ?? def.Id;
            string tooltip = BuildTooltip(def);
            if (def.Type == SettingType.Color)
            {
                tooltip = AppendTooltip(tooltip, ColorPreviewTooltip);
            }

            FieldInfo field = null;
            if (!string.IsNullOrEmpty(def.FieldName))
            {
                field = settingsObject.GetType().GetField(def.FieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            bool reserveResetSlot = ShowResetIcons && IsResettable(def, field);
            if (reserveResetSlot)
            {
                Rect resetRect = new Rect(
                    contentRect.x,
                    contentRect.y + ((contentRect.height - ResetButtonSize) / 2f),
                    ResetButtonSize,
                    ResetButtonSize);

                contentRect.x += ResetIconSlotWidth;
                contentRect.width = Mathf.Max(0f, contentRect.width - ResetIconSlotWidth);

                if (HasNonDefaultValue(field, settingsObject, def))
                {
                    DrawResetButton(resetRect, disabled, () =>
                    {
                        if (ResetSettingToDefault(field, settingsObject, def))
                        {
                            HandleSettingChanged(def, settingsObject, onSettingsChanged);
                        }
                    });
                }
            }

            switch (def.Type)
            {
                case SettingType.Bool:
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        bool boolValue = (bool)field.GetValue(settingsObject);
                        bool changed = SettingWidgets.DrawBool(
                            contentRect,
                            label,
                            ref boolValue,
                            tooltip,
                            disabled);
                        if (changed)
                        {
                            field.SetValue(settingsObject, boolValue);
                            HandleSettingChanged(def, settingsObject, onSettingsChanged);
                        }
                    }
                    break;
                case SettingType.Color:
                    if (field != null && field.FieldType == typeof(Color))
                    {
                        Color colorValue = (Color)field.GetValue(settingsObject);
                        if (!disabled && Mouse.IsOver(controlRow))
                        {
                            ColorPreviewSink?.PreviewHover(def, colorValue);
                        }

                        SettingWidgets.DrawColor(contentRect, label, ref colorValue, tooltip, disabled,
                            (current, onSelected) =>
                            {
                                ColorPreviewSink?.BeginPicker(def, current);
                                bool previewEnded = false;
                                Action endPreview = () =>
                                {
                                    if (previewEnded)
                                    {
                                        return;
                                    }

                                    previewEnded = true;
                                    ColorPreviewSink?.EndPicker(def);
                                };
                                var dialog = new Spine.UI.ColourPicker.Dialog_ColourPicker(current, (newColor, _) =>
                                {
                                    field.SetValue(settingsObject, newColor);
                                    HandleSettingChanged(def, settingsObject, onSettingsChanged);
                                    onSelected?.Invoke(newColor);
                                }, previewCallback: newColor => ColorPreviewSink?.PreviewPicker(def, newColor));
                                dialog.onCancel = endPreview;
                                dialog.onPostClose = endPreview;

                                Find.WindowStack.Add(dialog);
                            }, EditColorLabel);
                    }
                    break;
                case SettingType.Enum:
                    if (field != null && def.EnumType != null)
                    {
                        object current = field.GetValue(settingsObject);
                        SettingWidgets.DrawEnum(contentRect, label, current, def.EnumType, tooltip, disabled, selected =>
                        {
                            field.SetValue(settingsObject, selected);
                            HandleSettingChanged(def, settingsObject, onSettingsChanged);
                        }, def.EnumLabelProvider, def.EnumDescriptionProvider);
                    }
                    break;
                case SettingType.Button:
                    if (SettingWidgets.DrawButton(contentRect, label, tooltip, disabled))
                    {
                        HandleSettingChanged(def, settingsObject, onSettingsChanged);
                    }
                    break;
                case SettingType.Header:
                    Color previousColor = GUI.color;
                    if (disabled)
                    {
                        GUI.color = Color.gray;
                    }

                    SettingWidgets.DrawHeader(contentRect, label);
                    GUI.color = previousColor;
                    break;
                case SettingType.Custom:
                    if (def.CustomDrawer != null &&
                        def.CustomDrawer(contentRect, label, tooltip, settingsObject, disabled))
                    {
                        HandleSettingChanged(def, settingsObject, onSettingsChanged);
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(tooltip) && !DescribedFloatMenu.AnyOpen)
            {
                TooltipHandler.TipRegion(controlRow, tooltip);
                if (Mouse.IsOver(controlRow))
                {
                    OnSettingTooltipViewed?.Invoke(def, settingsObject);
                }
            }
        }

        private static string AppendTooltip(string tooltip, string addition)
        {
            if (string.IsNullOrEmpty(addition))
            {
                return tooltip;
            }

            return string.IsNullOrEmpty(tooltip)
                ? addition
                : tooltip + "\n\n" + addition;
        }

        private float MeasureRowHeight(SettingDefinition def, object settingsObject)
        {
            return RowHeight;
        }

        private float MeasureTotalHeight(List<SettingDefinition> settings, object settingsObject)
        {
            float total = 0f;
            for (int i = 0; i < settings.Count; i++)
            {
                total += MeasureRowHeight(settings[i], settingsObject);
            }

            return total;
        }

        private string BuildTooltip(SettingDefinition def)
        {
            string tooltip = GetTooltip?.Invoke(def) ?? def.Tooltip ?? string.Empty;

            // Append parent chain info for children
            if (!string.IsNullOrEmpty(def.ParentId))
            {
                var parent = _hierarchy.GetParent(def);
                var grandParent = _hierarchy.GetParent(parent);

                List<string> parentParts = new List<string>();
                if (parent != null)
                {
                    parentParts.Add(GetLabel?.Invoke(parent) ?? parent.Label ?? parent.Id);
                }
                if (grandParent != null)
                {
                    parentParts.Add(GetLabel?.Invoke(grandParent) ?? grandParent.Label ?? grandParent.Id);
                }

                if (parentParts.Count > 0)
                {
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        tooltip += "\n\n";
                    }

                    tooltip += parentParts.Count == 1
                        ? $"Parent: {parentParts[0]}"
                        : $"Parent chain: {string.Join(" › ", parentParts)}";
                }
            }

            return tooltip;
        }

        private List<SettingDefinition> BuildVisibleSettings(
            object settingsObject,
            SettingsViewMode viewMode,
            bool useSearch)
        {
            return BuildVisibleSettings(settingsObject, viewMode, useSearch, _activeFilter, false);
        }

        private List<SettingDefinition> BuildVisibleSettings(
            object settingsObject,
            SettingsViewMode viewMode,
            bool useSearch,
            SettingsFilterDefinition filter,
            bool ignoreFilter)
        {
            IEnumerable<SettingDefinition> source = useSearch
                ? _hierarchy.Search(_searchQuery, viewMode, settingsObject, GetLabel, GetTooltip)
                : _hierarchy.GetFlattenedForView(viewMode, settingsObject);

            var visibleSettings = new List<SettingDefinition>();
            foreach (var setting in source)
            {
                if (settingsObject != null && setting.VisibleWhen != null && !setting.VisibleWhen(settingsObject))
                {
                    continue;
                }

                if (!ignoreFilter && !MatchesFilter(setting, settingsObject, filter))
                {
                    continue;
                }

                if (!useSearch &&
                    _hierarchy.IsDisabledByAncestor(setting, settingsObject) &&
                    !IsForcedVisibleThroughDisabledAncestor(setting))
                {
                    continue;
                }

                visibleSettings.Add(setting);
            }

            return visibleSettings;
        }

        private bool MatchesFilter(SettingDefinition setting, object settingsObject, SettingsFilterDefinition filter)
        {
            if (filter == null)
            {
                return true;
            }

            if (filter.Matches(setting, settingsObject))
            {
                return true;
            }

            if (!filter.IncludeChildrenOfMatches)
            {
                return false;
            }

            var parent = _hierarchy.GetParent(setting);
            while (parent != null)
            {
                if (filter.Matches(parent, settingsObject))
                {
                    return true;
                }

                parent = _hierarchy.GetParent(parent);
            }

            return false;
        }

        private void DrawEmptyState(Rect rect, object settingsObject, ref SettingsViewMode viewMode)
        {
            EmptyStateAction action = GetEmptyStateAction(settingsObject, viewMode);
            if (action != null)
            {
                if (DrawClickableEmptyState(rect, action.Label))
                {
                    if (action.SwitchToViewMode.HasValue)
                    {
                        viewMode = action.SwitchToViewMode.Value;
                    }

                    action.Action?.Invoke();
                }
                return;
            }

            Widgets.Label(rect, NoResultsLabel);
        }

        private EmptyStateAction GetEmptyStateAction(object settingsObject, SettingsViewMode viewMode)
        {
            bool isSearching = !string.IsNullOrWhiteSpace(_searchQuery);
            if (!isSearching)
            {
                if (_activeFilter != null)
                {
                    return new EmptyStateAction("Remove filter for more settings", ClearActiveFilter);
                }

                return null;
            }

            SettingsFilterDefinition suggestedFilter = FindSuggestedFilter(settingsObject, viewMode);
            if (suggestedFilter != null)
            {
                return new EmptyStateAction(
                    $"Change to {suggestedFilter.Label ?? suggestedFilter.Id} filter for those settings",
                    () => ApplySuggestedFilter(suggestedFilter, settingsObject, viewMode));
            }

            if (_activeFilter != null)
            {
                var unfilteredMatches = BuildVisibleSettings(
                    settingsObject,
                    viewMode,
                    useSearch: true,
                    filter: null,
                    ignoreFilter: true);
                if (unfilteredMatches.Count > 0)
                {
                    return new EmptyStateAction("Remove filter for more settings", ClearActiveFilter);
                }
            }

            if (viewMode == SettingsViewMode.Simple)
            {
                var advancedMatches = BuildVisibleSettings(
                    settingsObject,
                    SettingsViewMode.Advanced,
                    useSearch: true,
                    filter: _activeFilter,
                    ignoreFilter: false);
                if (advancedMatches.Count > 0)
                {
                    return EmptyStateAction.SwitchView(
                        "Switch to advanced mode for more settings",
                        SettingsViewMode.Advanced);
                }

                if (_activeFilter != null)
                {
                    var advancedUnfilteredMatches = BuildVisibleSettings(
                        settingsObject,
                        SettingsViewMode.Advanced,
                        useSearch: true,
                        filter: null,
                        ignoreFilter: true);
                    if (advancedUnfilteredMatches.Count > 0)
                    {
                        return new EmptyStateAction("Remove filter for more settings", ClearActiveFilter);
                    }
                }
            }

            return null;
        }

        private SettingsFilterDefinition FindSuggestedFilter(object settingsObject, SettingsViewMode viewMode)
        {
            if (Filters == null || Filters.Count == 0 || string.IsNullOrWhiteSpace(_searchQuery))
            {
                return null;
            }

            foreach (var filter in Filters)
            {
                if (filter == null || ReferenceEquals(filter, _activeFilter))
                {
                    continue;
                }

                if (FilterTextMatchesSearch(filter))
                {
                    return filter;
                }
            }

            foreach (var filter in Filters)
            {
                if (filter == null || ReferenceEquals(filter, _activeFilter))
                {
                    continue;
                }

                var matches = BuildVisibleSettings(
                    settingsObject,
                    viewMode,
                    useSearch: true,
                    filter: filter,
                    ignoreFilter: false);
                if (matches.Count > 0)
                {
                    return filter;
                }
            }

            return null;
        }

        private bool FilterTextMatchesSearch(SettingsFilterDefinition filter)
        {
            string needle = (_searchQuery ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(needle) || filter == null)
            {
                return false;
            }

            string text = $"{filter.Id} {filter.Label} {filter.Tooltip} {filter.Category} {filter.CategoryLabel}".ToLowerInvariant();
            return text.Contains(needle);
        }

        private void ApplySuggestedFilter(
            SettingsFilterDefinition filter,
            object settingsObject,
            SettingsViewMode viewMode)
        {
            ApplyFilter(filter);
            var matchesWithSearch = BuildVisibleSettings(
                settingsObject,
                viewMode,
                useSearch: true,
                filter: filter,
                ignoreFilter: false);
            if (matchesWithSearch.Count == 0)
            {
                ClearSearch();
            }
        }

        private bool DrawClickableEmptyState(Rect rect, string label)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width, 28f);
            Color oldColor = GUI.color;
            Event evt = Event.current;
            bool hovered = evt != null && labelRect.Contains(evt.mousePosition);
            GUI.color = hovered ? Color.white : new Color(0.8f, 0.85f, 1f);
            Widgets.Label(labelRect, label);
            Vector2 size = Text.CalcSize(label);
            float underlineWidth = Mathf.Min(size.x, labelRect.width);
            Widgets.DrawLineHorizontal(labelRect.x, labelRect.y + size.y + 1f, underlineWidth);
            GUI.color = oldColor;

            if (Widgets.ButtonInvisible(labelRect))
            {
                Event.current?.Use();
                return true;
            }

            return false;
        }

        private void DrawClearFilterRow(Rect rect)
        {
            Rect buttonRect = rect.ContractedBy(4f);
            if (Widgets.ButtonText(buttonRect, "X Clear filter"))
            {
                ClearActiveFilter();
                Event.current?.Use();
            }
        }

        private void ApplyFilter(SettingsFilterDefinition filter)
        {
            _activeFilter = filter;
            _scrollPosition = Vector2.zero;
        }

        private void ClearActiveFilter()
        {
            if (_activeFilter == null)
            {
                return;
            }

            ApplyFilter(null);
        }

        private bool TryHandleSearchResultDoubleClick(
            Rect rowRect,
            SettingDefinition target,
            object settingsObject,
            SettingsViewMode viewMode,
            float listHeight)
        {
            Event evt = Event.current;
            if (evt == null ||
                evt.type != EventType.MouseDown ||
                evt.button != 0 ||
                !rowRect.Contains(evt.mousePosition))
            {
                return false;
            }

            string targetId = target?.Id;
            float now = Time.realtimeSinceStartup;
            bool sameTarget = !string.IsNullOrEmpty(targetId) &&
                string.Equals(_lastSearchClickSettingId, targetId, StringComparison.OrdinalIgnoreCase);
            bool quickEnough = _lastSearchClickTime >= 0f &&
                now - _lastSearchClickTime <= SearchResultDoubleClickMaxSeconds;
            bool closeEnough =
                Vector2.Distance(_lastSearchClickPosition, evt.mousePosition) <= SearchResultDoubleClickMoveTolerance;

            if (!sameTarget || !quickEnough || !closeEnough || evt.clickCount != 2)
            {
                _lastSearchClickSettingId = targetId;
                _lastSearchClickTime = now;
                _lastSearchClickPosition = evt.mousePosition;
                return false;
            }

            _lastSearchClickSettingId = null;
            _lastSearchClickTime = -1f;
            RevealDisabledAncestorChain(target.Id);
            CenterOnSetting(target, settingsObject, viewMode, listHeight);
            FocusSetting(target?.Id);
            ClearSearch();
            evt.Use();
            return true;
        }

        private void ClearSearch()
        {
            _searchWidget.Reset();
            _searchWidget.Unfocus();
            _searchQuery = string.Empty;
            _lastSearchClickSettingId = null;
            _lastSearchClickTime = -1f;
        }

        private void FocusSetting(string settingId)
        {
            if (string.IsNullOrEmpty(settingId))
            {
                return;
            }

            _highlightedSettingId = settingId;
            _highlightStartedAt = Time.realtimeSinceStartup;
        }

        private void RevealDisabledAncestorChain(string settingId)
        {
            SettingDefinition setting = _hierarchy.GetById(settingId);
            if (setting == null)
            {
                return;
            }

            foreach (SettingDefinition ancestor in _hierarchy.GetAncestors(setting))
            {
                if (!ancestor.ControlsChildVisibility || ancestor.Type != SettingType.Bool)
                {
                    continue;
                }

                _forceVisibleDisabledAncestorIds.Add(ancestor.Id);
            }
        }

        private bool IsForcedVisibleThroughDisabledAncestor(SettingDefinition setting)
        {
            if (setting == null || _forceVisibleDisabledAncestorIds.Count == 0)
            {
                return false;
            }

            foreach (SettingDefinition ancestor in _hierarchy.GetAncestors(setting))
            {
                if (_forceVisibleDisabledAncestorIds.Contains(ancestor.Id))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFocusedForcedVisibleSetting(SettingDefinition setting)
        {
            return setting != null &&
                !string.IsNullOrEmpty(_highlightedSettingId) &&
                string.Equals(setting.Id, _highlightedSettingId, StringComparison.OrdinalIgnoreCase) &&
                IsForcedVisibleThroughDisabledAncestor(setting);
        }

        private void HandleSettingChanged(
            SettingDefinition changedSetting,
            object settingsObject,
            Action onSettingsChanged)
        {
            ClearForcedVisibilityForControlledChildren(changedSetting);
            bool ancestorsChanged = EnableControllingAncestors(changedSetting, settingsObject);
            changedSetting?.OnChanged?.Invoke(settingsObject);
            onSettingsChanged?.Invoke();

            if (ancestorsChanged)
            {
                _forceVisibleDisabledAncestorIds.Clear();
            }
        }

        private bool EnableControllingAncestors(SettingDefinition setting, object settingsObject)
        {
            if (setting == null || settingsObject == null)
            {
                return false;
            }

            bool changed = false;
            foreach (SettingDefinition ancestor in _hierarchy.GetAncestors(setting))
            {
                if (!ancestor.ControlsChildVisibility ||
                    ancestor.Type != SettingType.Bool ||
                    string.IsNullOrEmpty(ancestor.FieldName))
                {
                    continue;
                }

                FieldInfo field = settingsObject.GetType().GetField(
                    ancestor.FieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(bool) || (bool)field.GetValue(settingsObject))
                {
                    continue;
                }

                field.SetValue(settingsObject, true);
                ancestor.OnChanged?.Invoke(settingsObject);
                changed = true;
            }

            return changed;
        }

        private void ClearForcedVisibilityForControlledChildren(SettingDefinition setting)
        {
            if (setting == null || !setting.ControlsChildVisibility || setting.Type != SettingType.Bool)
            {
                return;
            }

            _forceVisibleDisabledAncestorIds.Remove(setting.Id);
            ClearForcedVisibilityForDescendants(setting.Id);
        }

        private void ClearForcedVisibilityForDescendants(string parentId)
        {
            var children = _hierarchy.GetChildren(parentId);
            for (int i = 0; i < children.Count; i++)
            {
                SettingDefinition child = children[i];
                if (child == null)
                {
                    continue;
                }

                _forceVisibleDisabledAncestorIds.Remove(child.Id);
                ClearForcedVisibilityForDescendants(child.Id);
            }
        }

        private void DrawFocusedSettingHighlight(Rect rowRect, SettingDefinition def)
        {
            if (def == null ||
                string.IsNullOrEmpty(_highlightedSettingId) ||
                !string.Equals(def.Id, _highlightedSettingId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            float age = Time.realtimeSinceStartup - _highlightStartedAt;
            if (!ContextualPresentationMath.IsHighlightActive(
                Time.realtimeSinceStartup,
                _highlightStartedAt,
                FocusHighlightSeconds))
            {
                _highlightedSettingId = null;
                return;
            }

            float fade = 1f - Mathf.Clamp01(age / FocusHighlightSeconds);
            float pulse = 0.5f + (0.5f * Mathf.Sin(age * 16f));
            Color focusColor = FocusHighlightColor;
            Color oldColor = GUI.color;
            GUI.color = new Color(focusColor.r, focusColor.g, focusColor.b, Mathf.Lerp(0.18f, 0.36f, pulse) * fade);
            Widgets.DrawBoxSolid(rowRect, GUI.color);
            GUI.color = new Color(focusColor.r, focusColor.g, focusColor.b, 0.85f * fade);
            Widgets.DrawBox(rowRect, 2);
            GUI.color = oldColor;
        }

        private void CenterOnSetting(
            SettingDefinition target,
            object settingsObject,
            SettingsViewMode viewMode,
            float listHeight)
        {
            if (target == null)
            {
                return;
            }

            var fullList = BuildVisibleSettings(settingsObject, viewMode, useSearch: false);
            int index = fullList.FindIndex(def => ReferenceEquals(def, target) || def.Id == target.Id);
            ScrollToIndex(fullList, settingsObject, index, listHeight);
        }

        private void CenterOnSettingId(
            string settingId,
            List<SettingDefinition> visibleSettings,
            object settingsObject,
            float listHeight)
        {
            if (string.IsNullOrEmpty(settingId) || visibleSettings == null)
            {
                return;
            }

            int index = visibleSettings.FindIndex(def =>
                string.Equals(def.Id, settingId, StringComparison.OrdinalIgnoreCase));
            ScrollToIndex(visibleSettings, settingsObject, index, listHeight);
        }

        /// <summary>
        /// Scrolls so the row at <paramref name="index"/> sits in the middle of the viewport.
        /// Rows are not uniform height, so offsets are accumulated rather than multiplied.
        /// </summary>
        private void ScrollToIndex(
            List<SettingDefinition> settings,
            object settingsObject,
            int index,
            float listHeight)
        {
            if (settings == null || index < 0 || index >= settings.Count)
            {
                return;
            }

            float targetY = 0f;
            float viewHeight = 0f;
            for (int i = 0; i < settings.Count; i++)
            {
                float rowHeight = MeasureRowHeight(settings[i], settingsObject);
                if (i < index)
                {
                    targetY += rowHeight;
                }

                viewHeight += rowHeight;
            }

            _scrollPosition.y = ContextualPresentationMath.CenteredScroll(
                targetY,
                listHeight,
                RowHeight,
                viewHeight);
            _scrollPosition.x = 0f;
        }

        private bool IsResettable(SettingDefinition def, FieldInfo field)
        {
            if (def == null)
            {
                return false;
            }

            if (field == null || def.DefaultValue == null)
            {
                return false;
            }

            switch (def.Type)
            {
                case SettingType.Bool:
                case SettingType.Color:
                case SettingType.Enum:
                    return true;
                default:
                    return false;
            }
        }

        private bool HasNonDefaultValue(FieldInfo field, object settingsObject, SettingDefinition def)
        {
            if (settingsObject == null || def == null)
            {
                return false;
            }

            if (field == null)
            {
                return false;
            }

            if (!TryGetDefaultValueForField(field, def.DefaultValue, out var defaultValue))
            {
                return false;
            }

            object currentValue = field.GetValue(settingsObject);
            return !ValuesEqual(currentValue, defaultValue);
        }

        private void DrawResetButton(Rect rect, bool disabled, Action resetAction)
        {
            bool oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            if (disabled)
            {
                GUI.enabled = false;
                GUI.color = Color.gray;
            }

            bool clicked = Widgets.ButtonText(rect, "R");

            GUI.enabled = oldEnabled;
            GUI.color = oldColor;

            TooltipHandler.TipRegion(rect, ResetToDefaultLabel);
            if (!disabled && clicked)
            {
                resetAction?.Invoke();
                Event.current?.Use();
            }
        }

        private bool ResetSettingToDefault(FieldInfo field, object settingsObject, SettingDefinition def)
        {
            if (settingsObject == null || def == null)
            {
                return false;
            }

            if (field == null)
            {
                return false;
            }

            if (!TryGetDefaultValueForField(field, def.DefaultValue, out var defaultValue))
            {
                return false;
            }

            field.SetValue(settingsObject, defaultValue);
            return true;
        }

        private static bool TryGetDefaultValueForField(FieldInfo field, object configuredDefault, out object value)
        {
            value = null;
            if (field == null || configuredDefault == null)
            {
                return false;
            }

            Type fieldType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            Type defaultType = configuredDefault.GetType();

            try
            {
                if (fieldType.IsAssignableFrom(defaultType))
                {
                    value = configuredDefault;
                    return true;
                }

                if (fieldType.IsEnum)
                {
                    value = configuredDefault is string text
                        ? Enum.Parse(fieldType, text)
                        : Enum.ToObject(fieldType, configuredDefault);
                    return true;
                }

                value = Convert.ChangeType(configuredDefault, fieldType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ValuesEqual(object a, object b)
        {
            if (a is float af && b is float bf)
            {
                return Mathf.Approximately(af, bf);
            }

            if (a is double ad && b is double bd)
            {
                return Math.Abs(ad - bd) < 0.0001d;
            }

            if (a is Color ac && b is Color bc)
            {
                return Mathf.Approximately(ac.r, bc.r) &&
                       Mathf.Approximately(ac.g, bc.g) &&
                       Mathf.Approximately(ac.b, bc.b) &&
                       Mathf.Approximately(ac.a, bc.a);
            }

            return Equals(a, b);
        }


        private sealed class EmptyStateAction
        {
            internal readonly string Label;
            internal readonly Action Action;
            internal readonly SettingsViewMode? SwitchToViewMode;

            internal EmptyStateAction(string label, Action action)
            {
                Label = label;
                Action = action;
            }

            private EmptyStateAction(string label, SettingsViewMode switchToViewMode)
            {
                Label = label;
                SwitchToViewMode = switchToViewMode;
            }

            internal static EmptyStateAction SwitchView(string label, SettingsViewMode viewMode)
            {
                return new EmptyStateAction(label, viewMode);
            }
        }
    }
}

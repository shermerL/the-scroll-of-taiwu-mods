using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Config;
using GameData.Domains.Item;
using GameData.Utilities;
using UnityEngine;
using UnityEngine.UI;

public sealed class ItemAdderWindow : MonoBehaviour
{
    private static readonly string[] TypeNames =
    {
        "武器", "护甲", "饰品", "衣着", "代步", "材料", "工具",
        "食物", "药品/毒物", "茶酒", "书籍", "促织", "杂物"
    };

    private static readonly string[] GradeNames =
    {
        "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品"
    };

    private static readonly string[] GradeColors =
    {
        "#8f8f8f", "#dadada", "#6fb96f", "#5ea9dc", "#b083e6", "#d89a46", "#e1c35b", "#f08b8b", "#ff5f5f"
    };

    private const string SettingsFileName = "TaiwuJiaJiaJia.settings.ini";
    private const string SettingsDirectoryName = "TaiwuJiaJiaJia";
    private const string CacheFileName = "ItemCache.tsv";
    private const string CacheHeader = "TaiwuItemAdderFrontend.ItemCache.v3";
    private const int QuantityStep = 10;
    private const float CardHeight = 144f;
    private const float CardMinWidth = 150f;
    private const float CardGap = 8f;
    private const float GridHeight = 450f;
    private const float DetailPanelWidth = 292f;
    private const float MainDetailGap = 12f;

    private static readonly List<ItemEntry> CachedItems = new List<ItemEntry>();
    private static bool _staticCacheLoaded;
    private static string _staticCacheStatus;

    private readonly List<ItemEntry> _items = new List<ItemEntry>();
    private readonly List<ItemEntry> _filtered = new List<ItemEntry>();
    private readonly Dictionary<string, Sprite> _iconSprites = new Dictionary<string, Sprite>();
    private readonly HashSet<string> _requestedIconNames = new HashSet<string>();
    private ModSettings _settings = ModSettings.CreateDefault();
    private GameObject _inputBlockerRoot;
    private Rect _windowRect = new Rect(40f, 55f, 1280f, 760f);
    private Vector2 _scroll;
    private Vector2 _detailScroll;
    private Vector2 _dragOffset;
    private KeyCode _toggleKey = KeyCode.Home;
    private bool _visible;
    private bool _showBlocked;
    private bool _captureHotkey;
    private bool _draggingWindow;
    private int _selectedType = -1;
    private int _selectedGrade = -1;
    private ItemEntry _selectedItem;
    private string _search = string.Empty;
    private string _amountText = "10";
    private string _status = "Home 呼出/隐藏。默认过滤剧情、任务、特殊物品。";
    private double _lastRefreshTime;
    private GUIStyle _rowStyle;
    private GUIStyle _rowAltStyle;
    private GUIStyle _selectedRowStyle;
    private GUIStyle _cardTitleStyle;
    private GUIStyle _cardMetaStyle;
    private GUIStyle _iconPlaceholderStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _tabStyle;
    private GUIStyle _tabSelectedStyle;
    private GUIStyle _primaryButtonStyle;
    private GUIStyle _secondaryButtonStyle;
    private GUIStyle _textFieldStyle;
    private GUIStyle _listBoxStyle;
    private GUIStyle _windowStyle;
    private GUIStyle _detailTitleStyle;
    private GUIStyle _detailLabelStyle;
    private GUIStyle _warningBadgeStyle;

    private void Awake()
    {
        _settings = ModSettings.Load();
        _toggleKey = _settings.ToggleKey;
        _status = $"当前呼出/隐藏热键：{_toggleKey}。默认过滤剧情、任务、特殊物品。";
        CreateInputBlocker();
        LoadInitialCache();
    }

    private void OnDestroy()
    {
        if (_inputBlockerRoot != null)
        {
            Destroy(_inputBlockerRoot);
            _inputBlockerRoot = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            ToggleWindow();
        }
    }

    private void OnGUI()
    {
        if (_captureHotkey)
        {
            CaptureHotkey(Event.current);
        }

        if (!_visible)
        {
            return;
        }

        EnsureStyles();
        GUI.depth = -1000;
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "太吾加加加", _windowStyle);
    }

    private void ToggleWindow()
    {
        _visible = !_visible;
        if (_visible)
        {
            RefreshItemsIfNeeded(force: false);
        }
        else
        {
            _captureHotkey = false;
        }

        SetInputBlockerVisible(_visible);
    }

    private void CreateInputBlocker()
    {
        try
        {
            _inputBlockerRoot = new GameObject("TaiwuJiaJiaJia.InputBlocker");
            _inputBlockerRoot.transform.SetParent(transform, worldPositionStays: false);

            Canvas canvas = _inputBlockerRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            _inputBlockerRoot.AddComponent<CanvasScaler>();
            _inputBlockerRoot.AddComponent<GraphicRaycaster>();

            CanvasGroup group = _inputBlockerRoot.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            GameObject imageObject = new GameObject("RaycastTarget");
            imageObject.transform.SetParent(_inputBlockerRoot.transform, worldPositionStays: false);
            RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = imageObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            SetInputBlockerVisible(visible: false);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("创建输入遮罩层失败，底层点击可能仍会穿透。", ex);
            _inputBlockerRoot = null;
        }
    }

    private void SetInputBlockerVisible(bool visible)
    {
        if (_inputBlockerRoot != null)
        {
            _inputBlockerRoot.SetActive(visible);
        }
    }

    private void DrawWindow(int id)
    {
        HandleWindowDrag();
        Rect content = new Rect(16f, 28f, _windowRect.width - 32f, _windowRect.height - 44f);
        GUI.Box(new Rect(8f, 24f, _windowRect.width - 16f, _windowRect.height - 32f), GUIContent.none, _listBoxStyle);
        float leftWidth = content.width - DetailPanelWidth - MainDetailGap;
        float detailX = content.x + leftWidth + MainDetailGap;
        float y = content.y;

        GUI.Label(new Rect(content.x, y, 50f, 22f), "分类");
        DrawTypeButtons(new Rect(content.x + 54f, y, leftWidth - 54f, 52f));
        y += 58f;

        GUI.Label(new Rect(content.x, y, 50f, 22f), "品级");
        DrawGradeButtons(new Rect(content.x + 54f, y, leftWidth - 54f, 24f));
        y += 32f;

        GUI.Label(new Rect(content.x, y + 2f, 50f, 24f), "搜索");
        Rect searchRect = new Rect(content.x + 54f, y, leftWidth - 54f, 28f);
        GUI.SetNextControlName("TaiwuJiaJiaJia.Search");
        string nextSearch = GUI.TextField(searchRect, _search ?? string.Empty, _textFieldStyle);
        if (string.IsNullOrEmpty(_search) && GUI.GetNameOfFocusedControl() != "TaiwuJiaJiaJia.Search")
        {
            GUI.Label(new Rect(searchRect.x + 10f, searchRect.y + 4f, searchRect.width - 20f, 22f), "名称或ID", _mutedStyle);
        }
        if (nextSearch != _search)
        {
            _search = nextSearch;
            RebuildFiltered();
        }

        bool nextShowBlocked = GUI.Toggle(new Rect(detailX, y + 2f, 178f, 26f), _showBlocked, "显示任务/特殊物品");
        if (nextShowBlocked != _showBlocked)
        {
            _showBlocked = nextShowBlocked;
            RebuildFiltered();
        }

        if (GUI.Button(new Rect(detailX + DetailPanelWidth - 72f, y, 72f, 28f), "刷新", _secondaryButtonStyle))
        {
            RefreshItemsIfNeeded(force: true);
        }
        y += 36f;

        float listTopY = y;
        GUI.Label(new Rect(content.x, y, 240f, 22f), $"物品列表：{_filtered.Count} / {_items.Count}");
        GUI.Label(new Rect(content.x + 250f, y, leftWidth - 250f, 22f), GetSelectedText(), _mutedStyle);
        y += 24f;

        DrawVirtualGrid(new Rect(content.x, y, leftWidth, GridHeight));
        DrawDetailPanel(new Rect(detailX, listTopY, DetailPanelWidth, GridHeight + 68f));
        y += GridHeight + 10f;

        GUI.Label(new Rect(content.x, y, 42f, 24f), "数量");
        if (GUI.Button(new Rect(content.x + 46f, y, 34f, 26f), "<", _secondaryButtonStyle))
        {
            StepAmount(-QuantityStep);
        }
        _amountText = GUI.TextField(new Rect(content.x + 84f, y, 78f, 26f), _amountText, _textFieldStyle);
        if (GUI.Button(new Rect(content.x + 166f, y, 34f, 26f), ">", _secondaryButtonStyle))
        {
            StepAmount(QuantityStep);
        }

        GUI.enabled = _selectedItem != null && !_selectedItem.Blocked;
        if (GUI.Button(new Rect(content.x + 214f, y, 220f, 26f), "添加选中物品", _primaryButtonStyle))
        {
            AddSelected();
        }
        GUI.enabled = true;

        Rect hotkeyLabelRect = new Rect(content.x + leftWidth - 264f, y, 144f, 26f);
        GUI.Label(hotkeyLabelRect, $"当前热键：{_toggleKey}", _mutedStyle);
        string keyButtonText = _captureHotkey ? "按键中..." : "修改热键";
        if (GUI.Button(new Rect(content.x + leftWidth - 112f, y, 112f, 26f), keyButtonText, _secondaryButtonStyle))
        {
            _captureHotkey = true;
            _status = "请按一个键作为新的呼出/隐藏热键，Esc 取消。";
        }
        y += 34f;

        GUI.Label(new Rect(content.x, y, leftWidth, 44f), _status, _mutedStyle);
    }

    private void HandleWindowDrag()
    {
        Event current = Event.current;
        if (current == null)
        {
            return;
        }

        Rect dragRect = new Rect(0f, 0f, _windowRect.width, 28f);
        if (current.type == EventType.MouseDown && current.button == 0 && dragRect.Contains(current.mousePosition))
        {
            _draggingWindow = true;
            _dragOffset = current.mousePosition;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && _draggingWindow)
        {
            Vector2 mouseScreen = GUIUtility.GUIToScreenPoint(current.mousePosition);
            _windowRect.x = mouseScreen.x - _dragOffset.x;
            _windowRect.y = mouseScreen.y - _dragOffset.y;
            ClampWindowToScreen();
            current.Use();
        }
        else if (current.type == EventType.MouseUp && _draggingWindow)
        {
            _draggingWindow = false;
            current.Use();
        }
    }

    private void ClampWindowToScreen()
    {
        float maxX = Mathf.Max(0f, Screen.width - _windowRect.width);
        float maxY = Mathf.Max(0f, Screen.height - _windowRect.height);
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, maxX);
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, maxY);
    }

    private void DrawTypeButtons(Rect area)
    {
        DrawButtonGrid(area, TypeNames, _selectedType, 7, 72f, 24f, index =>
        {
            _selectedType = index;
            _selectedItem = null;
            RebuildFiltered();
        }, includeAll: true);
    }

    private void DrawGradeButtons(Rect area)
    {
        DrawButtonGrid(area, GradeNames, _selectedGrade, 10, 62f, 24f, index =>
        {
            _selectedGrade = index;
            _selectedItem = null;
            RebuildFiltered();
        }, includeAll: true);
    }

    private void DrawButtonGrid(Rect area, string[] labels, int selected, int columns, float width, float height, Action<int> onSelect, bool includeAll)
    {
        int offset = includeAll ? 1 : 0;
        int count = labels.Length + offset;
        for (int i = 0; i < count; i++)
        {
            int value = i - offset;
            int col = i % columns;
            int row = i / columns;
            Rect rect = new Rect(area.x + col * width, area.y + row * height, width - 4f, height - 2f);
            string label = value < 0 ? "全部" : labels[value];
            bool isSelected = selected == value;
            if (GUI.Button(rect, label, isSelected ? _tabSelectedStyle : _tabStyle))
            {
                onSelect(value);
            }
        }
    }

    private void DrawVirtualGrid(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, _listBoxStyle);
        int columns = Mathf.Max(1, Mathf.FloorToInt((rect.width - 22f + CardGap) / (CardMinWidth + CardGap)));
        float cardWidth = Mathf.Floor((rect.width - 22f - CardGap * (columns - 1)) / columns);
        int rowCount = Mathf.CeilToInt(_filtered.Count / (float)columns);
        Rect view = new Rect(0f, 0f, rect.width - 22f, Mathf.Max(rowCount * (CardHeight + CardGap), rect.height));
        _scroll = GUI.BeginScrollView(rect, _scroll, view);

        int firstRow = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / (CardHeight + CardGap)));
        int visibleRows = Mathf.CeilToInt(rect.height / (CardHeight + CardGap)) + 2;
        int first = firstRow * columns;
        int last = Mathf.Min(_filtered.Count, (firstRow + visibleRows) * columns);

        for (int i = first; i < last; i++)
        {
            ItemEntry item = _filtered[i];
            int gridRow = i / columns;
            int gridCol = i % columns;
            Rect card = new Rect(
                4f + gridCol * (cardWidth + CardGap),
                gridRow * (CardHeight + CardGap),
                cardWidth - 4f,
                CardHeight);
            GUIStyle style = item == _selectedItem ? _selectedRowStyle : ((gridRow + gridCol & 1) == 0 ? _rowStyle : _rowAltStyle);
            if (GUI.Button(card, GUIContent.none, style))
            {
                _selectedItem = item;
                _status = item.Blocked ? $"已选中但不可添加：{item.BlockReason}" : $"已选中：{item.Name}";
            }
            DrawItemCardContent(card, item);
        }

        GUI.EndScrollView();
    }

    private void DrawItemCardContent(Rect card, ItemEntry item)
    {
        string color = GetGradeColorText(item.Grade);
        string grade = GetGradeName(item.Grade);
        Rect iconRect = new Rect(card.x + (card.width - 72f) * 0.5f, card.y + 10f, 72f, 72f);
        DrawIcon(iconRect, item.Icon);

        Rect titleRect = new Rect(card.x + 8f, card.y + 84f, card.width - 16f, 24f);
        GUI.Label(titleRect, $"<color={color}>{item.Name}</color>", _cardTitleStyle);

        Rect metaRect = new Rect(card.x + 8f, card.y + 108f, card.width - 16f, 18f);
        GUI.Label(metaRect, $"{item.TypeName} / {grade}", _cardMetaStyle);

        if (item.Blocked)
        {
            Rect warningRect = new Rect(card.x + card.width - 30f, card.y + 8f, 22f, 22f);
            GUI.Label(warningRect, "!", _warningBadgeStyle);
            Rect blockedRect = new Rect(card.x + 8f, card.y + 124f, card.width - 16f, 16f);
            GUI.Label(blockedRect, $"<color=#f2c15b>不可添加：{item.BlockReason}</color>", _cardMetaStyle);
        }
    }

    private void DrawIcon(Rect rect, string iconName)
    {
        Sprite sprite = GetIconSprite(iconName);
        if (sprite == null || sprite.texture == null)
        {
            GUI.Box(rect, "图", _iconPlaceholderStyle);
            return;
        }

        GUI.Box(rect, GUIContent.none, _iconPlaceholderStyle);
        Texture2D texture = sprite.texture;
        Rect tr = sprite.textureRect;
        Rect uv = new Rect(tr.x / texture.width, tr.y / texture.height, tr.width / texture.width, tr.height / texture.height);
        Rect inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
        GUI.DrawTextureWithTexCoords(inner, texture, uv, alphaBlend: true);
    }

    private Sprite GetIconSprite(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            return null;
        }

        if (_iconSprites.TryGetValue(iconName, out Sprite sprite))
        {
            return sprite;
        }

        if (AtlasInfo.Instance == null || _requestedIconNames.Contains(iconName))
        {
            return null;
        }

        _requestedIconNames.Add(iconName);
        try
        {
            AtlasInfo.Instance.GetSprite(iconName, loaded =>
            {
                if (loaded != null)
                {
                    _iconSprites[iconName] = loaded;
                }
            });
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"读取物品图标失败：icon={iconName}", ex);
        }

        return _iconSprites.TryGetValue(iconName, out sprite) ? sprite : null;
    }

    private void DrawDetailPanel(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, _listBoxStyle);
        Rect inner = new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f);
        if (_selectedItem == null)
        {
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 24f), "未选择物品", _detailTitleStyle);
            GUI.Label(new Rect(inner.x, inner.y + 30f, inner.width, 60f), "点击左侧物品后，此处会显示品级、分类、基础价值与说明。", _mutedStyle);
            return;
        }

        ItemEntry item = _selectedItem;
        Rect view = new Rect(0f, 0f, inner.width - 18f, 620f);
        _detailScroll = GUI.BeginScrollView(inner, _detailScroll, view);

        float y = 0f;
        Rect iconRect = new Rect((view.width - 92f) * 0.5f, y, 92f, 92f);
        DrawIcon(iconRect, item.Icon);
        y += 102f;

        string color = GetGradeColorText(item.Grade);
        GUI.Label(new Rect(0f, y, view.width, 28f), $"<color={color}>{item.Name}</color>", _detailTitleStyle);
        y += 34f;

        if (item.Blocked)
        {
            GUI.Label(new Rect(0f, y, view.width, 42f), $"! 不允许添加：{item.BlockReason}", _warningBadgeStyle);
            y += 48f;
        }

        DrawDetailLine(ref y, "分类", item.TypeName);
        DrawDetailLine(ref y, "品级", GetGradeName(item.Grade));
        DrawDetailLine(ref y, "模板ID", item.TemplateId.ToString());
        DrawDetailLine(ref y, "类型ID", item.ItemType.ToString());
        if (item.ItemSubType >= 0)
        {
            DrawDetailLine(ref y, "细类", item.ItemSubType.ToString());
        }
        if (item.BaseValue > 0)
        {
            DrawDetailLine(ref y, "基础价值", item.BaseValue.ToString());
        }
        if (item.BaseWeight > 0)
        {
            DrawDetailLine(ref y, "基础重量", item.BaseWeight.ToString());
        }
        if (item.Duration > 0)
        {
            DrawDetailLine(ref y, "持续时间", item.Duration.ToString());
        }
        if (item.MaxUseDistance > 0)
        {
            DrawDetailLine(ref y, "使用距离", item.MaxUseDistance.ToString());
        }
        DrawDetailLine(ref y, "可堆叠", item.Stackable ? "是" : "否");
        DrawDetailLine(ref y, "可转移", item.Transferable ? "是" : "否");
        DrawDetailLine(ref y, "添加状态", item.Blocked ? $"不可添加：{item.BlockReason}" : "可添加");

        if (!string.IsNullOrEmpty(item.Description))
        {
            y += 8f;
            GUI.Label(new Rect(0f, y, view.width, 22f), "说明", _detailTitleStyle);
            y += 26f;
            GUI.Label(new Rect(0f, y, view.width, 92f), item.Description, _detailLabelStyle);
            y += 98f;
        }

        if (!string.IsNullOrEmpty(item.FunctionDesc))
        {
            GUI.Label(new Rect(0f, y, view.width, 22f), "效果", _detailTitleStyle);
            y += 26f;
            GUI.Label(new Rect(0f, y, view.width, 120f), item.FunctionDesc, _detailLabelStyle);
        }

        GUI.EndScrollView();
    }

    private void DrawDetailLine(ref float y, string label, string value)
    {
        GUI.Label(new Rect(0f, y, 72f, 22f), label, _mutedStyle);
        GUI.Label(new Rect(78f, y, DetailPanelWidth - 122f, 22f), value, _detailLabelStyle);
        y += 24f;
    }

    private static string GetGradeName(sbyte grade)
    {
        try
        {
            if (grade >= 0 && grade < GradeNames.Length)
            {
                string gameText = ItemView.GetGradeText(grade);
                if (!string.IsNullOrEmpty(gameText))
                {
                    return gameText;
                }
            }
        }
        catch
        {
        }

        return grade >= 0 && grade < GradeNames.Length ? GradeNames[grade] : grade.ToString();
    }

    private static string GetGradeColorText(sbyte grade)
    {
        try
        {
            if (Colors.Instance != null && Colors.Instance.GradeColors != null &&
                grade >= 0 && grade < Colors.Instance.GradeColors.Length)
            {
                return "#" + ColorUtility.ToHtmlStringRGB(Colors.Instance.GradeColors[grade]);
            }
        }
        catch
        {
        }

        return grade >= 0 && grade < GradeColors.Length ? GradeColors[grade] : "#ffffff";
    }

    private string GetSelectedText()
    {
        if (_selectedItem == null)
        {
            return "未选择物品";
        }

        return $"当前：{_selectedItem.Name} / {_selectedItem.TypeName} / {GetGradeName(_selectedItem.Grade)}";
    }

    private void CaptureHotkey(Event current)
    {
        if (current == null || current.type != EventType.KeyDown)
        {
            return;
        }

        if (current.keyCode == KeyCode.Escape)
        {
            _captureHotkey = false;
            _status = "已取消设置热键。";
            current.Use();
            return;
        }

        if (current.keyCode == KeyCode.None)
        {
            return;
        }

        _toggleKey = current.keyCode;
        _settings.ToggleKey = _toggleKey;
        try
        {
            _settings.Save();
            _captureHotkey = false;
            _status = $"呼出/隐藏热键已改为：{_toggleKey}，已写入配置文件。";
        }
        catch (Exception ex)
        {
            _captureHotkey = false;
            _status = "保存热键失败，请查看日志。";
            ModLogger.Error($"保存热键失败：{_toggleKey}", ex);
        }
        current.Use();
    }

    private void EnsureStyles()
    {
        if (_rowStyle != null)
        {
            return;
        }

        _rowStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            richText = true,
            fontSize = 14,
            padding = new RectOffset(8, 8, 2, 2)
        };
        ApplyStateTextures(_rowStyle, new Color(0.18f, 0.14f, 0.10f, 1f), new Color(0.28f, 0.22f, 0.14f, 1f), new Color(0.38f, 0.28f, 0.14f, 1f));
        _rowStyle.normal.textColor = new Color(0.88f, 0.84f, 0.72f);
        _rowStyle.hover.textColor = new Color(1f, 0.94f, 0.76f);
        _rowStyle.active.textColor = Color.white;

        _rowAltStyle = new GUIStyle(_rowStyle);
        ApplyStateTextures(_rowAltStyle, new Color(0.21f, 0.16f, 0.11f, 1f), new Color(0.31f, 0.24f, 0.15f, 1f), new Color(0.42f, 0.31f, 0.16f, 1f));

        _selectedRowStyle = new GUIStyle(_rowStyle);
        ApplyStateTextures(_selectedRowStyle, new Color(0.50f, 0.36f, 0.13f, 1f), new Color(0.63f, 0.45f, 0.16f, 1f), new Color(0.76f, 0.56f, 0.22f, 1f));
        _selectedRowStyle.normal.textColor = Color.white;

        _mutedStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            richText = true
        };
        _mutedStyle.normal.textColor = new Color(0.87f, 0.82f, 0.70f);

        _cardTitleStyle = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontStyle = FontStyle.Bold,
            fontSize = 14,
            clipping = TextClipping.Clip
        };
        _cardTitleStyle.normal.textColor = Color.white;

        _cardMetaStyle = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = 12,
            clipping = TextClipping.Clip
        };
        _cardMetaStyle.normal.textColor = new Color(0.77f, 0.71f, 0.58f);

        _iconPlaceholderStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        ApplyStateTextures(_iconPlaceholderStyle, new Color(0.10f, 0.08f, 0.06f, 1f), new Color(0.14f, 0.11f, 0.07f, 1f), new Color(0.18f, 0.13f, 0.08f, 1f));
        _iconPlaceholderStyle.normal.textColor = new Color(0.66f, 0.59f, 0.43f);

        _tabStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            padding = new RectOffset(6, 6, 2, 2)
        };
        ApplyStateTextures(_tabStyle, new Color(0.17f, 0.25f, 0.21f, 1f), new Color(0.24f, 0.34f, 0.28f, 1f), new Color(0.36f, 0.38f, 0.24f, 1f));
        _tabStyle.normal.textColor = new Color(0.86f, 0.82f, 0.68f);
        _tabStyle.hover.textColor = Color.white;

        _tabSelectedStyle = new GUIStyle(_tabStyle);
        ApplyStateTextures(_tabSelectedStyle, new Color(0.57f, 0.40f, 0.13f, 1f), new Color(0.69f, 0.50f, 0.18f, 1f), new Color(0.80f, 0.60f, 0.24f, 1f));
        _tabSelectedStyle.normal.textColor = Color.white;

        _primaryButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 14
        };
        ApplyStateTextures(_primaryButtonStyle, new Color(0.46f, 0.30f, 0.12f, 1f), new Color(0.62f, 0.42f, 0.16f, 1f), new Color(0.76f, 0.54f, 0.22f, 1f));
        _primaryButtonStyle.normal.textColor = new Color(1f, 0.94f, 0.78f);
        _primaryButtonStyle.hover.textColor = Color.white;
        _primaryButtonStyle.active.textColor = Color.white;

        _secondaryButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13
        };
        ApplyStateTextures(_secondaryButtonStyle, new Color(0.20f, 0.24f, 0.20f, 1f), new Color(0.29f, 0.34f, 0.27f, 1f), new Color(0.42f, 0.35f, 0.18f, 1f));
        _secondaryButtonStyle.normal.textColor = new Color(0.86f, 0.82f, 0.68f);
        _secondaryButtonStyle.hover.textColor = Color.white;

        _textFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 14,
            padding = new RectOffset(8, 8, 3, 3)
        };
        ApplyStateTextures(_textFieldStyle, new Color(0.11f, 0.09f, 0.07f, 1f), new Color(0.15f, 0.12f, 0.08f, 1f), new Color(0.15f, 0.12f, 0.08f, 1f));
        _textFieldStyle.normal.textColor = new Color(0.94f, 0.90f, 0.78f);
        _textFieldStyle.focused.textColor = Color.white;

        _listBoxStyle = new GUIStyle(GUI.skin.box);
        _listBoxStyle.normal.background = MakeTex(new Color(0.12f, 0.09f, 0.07f, 1f));
        _listBoxStyle.normal.textColor = new Color(0.88f, 0.82f, 0.68f);

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(8, 8, 22, 8)
        };
        ApplyStateTextures(_windowStyle, new Color(0.16f, 0.11f, 0.08f, 1f), new Color(0.16f, 0.11f, 0.08f, 1f), new Color(0.16f, 0.11f, 0.08f, 1f));
        _windowStyle.normal.textColor = new Color(0.96f, 0.86f, 0.58f);
        _windowStyle.hover.textColor = _windowStyle.normal.textColor;
        _windowStyle.active.textColor = _windowStyle.normal.textColor;

        _detailTitleStyle = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        _detailTitleStyle.normal.textColor = new Color(0.96f, 0.86f, 0.58f);

        _detailLabelStyle = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = 13,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        _detailLabelStyle.normal.textColor = new Color(0.90f, 0.84f, 0.70f);

        _warningBadgeStyle = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        ApplyStateTextures(_warningBadgeStyle, new Color(0.45f, 0.18f, 0.10f, 1f), new Color(0.55f, 0.22f, 0.12f, 1f), new Color(0.55f, 0.22f, 0.12f, 1f));
        _warningBadgeStyle.normal.textColor = new Color(1f, 0.86f, 0.40f);
        _warningBadgeStyle.hover.textColor = _warningBadgeStyle.normal.textColor;
        _warningBadgeStyle.active.textColor = _warningBadgeStyle.normal.textColor;
    }

    private static void ApplyStateTextures(GUIStyle style, Color normal, Color hover, Color active)
    {
        style.normal.background = MakeTex(normal);
        style.hover.background = MakeTex(hover);
        style.active.background = MakeTex(active);
        style.focused.background = style.hover.background;
        style.onNormal.background = MakeTex(active);
        style.onHover.background = MakeTex(hover);
        style.onActive.background = MakeTex(active);
    }

    private static Texture2D MakeTex(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    private void RefreshItemsIfNeeded(bool force)
    {
        if (!force && _items.Count > 0)
        {
            return;
        }

        if (!force && _items.Count == 0 && CachedItems.Count > 0)
        {
            CopyCachedItemsToInstance();
            RebuildFiltered();
            _status = _staticCacheStatus ?? $"已从缓存载入 {_items.Count} 个物品配置。";
            return;
        }

        try
        {
            _items.Clear();
            foreach (IItemConfig config in ItemConfigHelper.AllConfigs)
            {
                if (config == null || config.TemplateId < 0)
                {
                    continue;
                }

                _items.Add(ItemEntry.FromConfig(config));
            }

            _items.Sort((a, b) =>
            {
                int typeCompare = a.ItemType.CompareTo(b.ItemType);
                if (typeCompare != 0)
                {
                    return typeCompare;
                }

                int gradeCompare = b.Grade.CompareTo(a.Grade);
                return gradeCompare != 0 ? gradeCompare : a.TemplateId.CompareTo(b.TemplateId);
            });
            _lastRefreshTime = Time.realtimeSinceStartupAsDouble;
            UpdateStaticCacheFromInstance();
            SaveDiskCache();
            RebuildFiltered();
            _status = $"已重建并缓存 {_items.Count} 个物品配置。";
        }
        catch (Exception ex)
        {
            _status = "载入物品配置失败：" + ex.Message;
            ModLogger.Error("扫描游戏物品配置失败。", ex);
        }
    }

    private void LoadInitialCache()
    {
        if (!_staticCacheLoaded)
        {
            LoadDiskCache();
        }

        if (CachedItems.Count == 0)
        {
            return;
        }

        CopyCachedItemsToInstance();
        RebuildFiltered();
        _status = _staticCacheStatus ?? $"已从缓存载入 {_items.Count} 个物品配置。";
    }

    private void CopyCachedItemsToInstance()
    {
        _items.Clear();
        _items.AddRange(CachedItems);
        _lastRefreshTime = Time.realtimeSinceStartupAsDouble;
    }

    private void UpdateStaticCacheFromInstance()
    {
        CachedItems.Clear();
        CachedItems.AddRange(_items);
        _staticCacheLoaded = true;
        _staticCacheStatus = $"已从内存缓存载入 {CachedItems.Count} 个物品配置。";
    }

    private static void LoadDiskCache()
    {
        _staticCacheLoaded = true;
        CachedItems.Clear();

        string path = GetCachePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _staticCacheStatus = null;
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0] != CacheHeader)
            {
                _staticCacheStatus = "物品缓存版本不匹配，请点击刷新重建。";
                ModLogger.Warn("物品缓存版本不匹配或文件为空，将等待用户刷新重建。");
                return;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                {
                    continue;
                }

                string[] parts = lines[i].Split('\t');
                if (parts.Length < 17)
                {
                    ModLogger.Warn($"跳过格式不完整的物品缓存行：line={i + 1}");
                    continue;
                }

                sbyte itemType = sbyte.Parse(parts[0]);
                short templateId = short.Parse(parts[1]);
                sbyte grade = sbyte.Parse(parts[2]);
                bool blocked = parts[3] == "1";
                string icon = Decode(parts[4]);
                string name = Decode(parts[5]);
                string blockReason = Decode(parts[6]);
                short itemSubType = short.Parse(parts[7]);
                short groupId = short.Parse(parts[8]);
                int baseValue = int.Parse(parts[9]);
                int baseWeight = int.Parse(parts[10]);
                short duration = short.Parse(parts[11]);
                sbyte maxUseDistance = sbyte.Parse(parts[12]);
                bool stackable = parts[13] == "1";
                bool transferable = parts[14] == "1";
                string description = Decode(parts[15]);
                string functionDesc = Decode(parts[16]);

                CachedItems.Add(new ItemEntry
                {
                    Name = name,
                    ItemType = itemType,
                    TemplateId = templateId,
                    Grade = grade,
                    Icon = icon,
                    ItemSubType = itemSubType,
                    GroupId = groupId,
                    BaseValue = baseValue,
                    BaseWeight = baseWeight,
                    Duration = duration,
                    MaxUseDistance = maxUseDistance,
                    Stackable = stackable,
                    Transferable = transferable,
                    Description = description,
                    FunctionDesc = functionDesc,
                    TypeName = itemType >= 0 && itemType < TypeNames.Length ? TypeNames[itemType] : "未知",
                    Blocked = blocked,
                    BlockReason = blockReason
                });
            }

            _staticCacheStatus = $"已从磁盘缓存载入 {CachedItems.Count} 个物品配置。";
        }
        catch (Exception ex)
        {
            CachedItems.Clear();
            _staticCacheStatus = "读取物品缓存失败，请点击刷新重建。";
            ModLogger.Warn("读取物品缓存失败。", ex);
        }
    }

    private static void SaveDiskCache()
    {
        string path = GetCachePath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            List<string> lines = new List<string>(CachedItems.Count + 1) { CacheHeader };
            foreach (ItemEntry item in CachedItems)
            {
                lines.Add(string.Join("\t", new[]
                {
                    item.ItemType.ToString(),
                    item.TemplateId.ToString(),
                    item.Grade.ToString(),
                    item.Blocked ? "1" : "0",
                    Encode(item.Icon),
                    Encode(item.Name),
                    Encode(item.BlockReason),
                    item.ItemSubType.ToString(),
                    item.GroupId.ToString(),
                    item.BaseValue.ToString(),
                    item.BaseWeight.ToString(),
                    item.Duration.ToString(),
                    item.MaxUseDistance.ToString(),
                    item.Stackable ? "1" : "0",
                    item.Transferable ? "1" : "0",
                    Encode(item.Description),
                    Encode(item.FunctionDesc)
                }));
            }

            File.WriteAllLines(path, lines.ToArray());
        }
        catch (Exception ex)
        {
            ModLogger.Warn("保存物品缓存失败。", ex);
        }
    }

    private static string GetCachePath()
    {
        string modRoot = GetModRoot();
        return string.IsNullOrEmpty(modRoot) ? null : Path.Combine(modRoot, CacheFileName);
    }

    private static string GetSettingsPath()
    {
        try
        {
            string persistentDataPath = Application.persistentDataPath;
            return string.IsNullOrWhiteSpace(persistentDataPath)
                ? null
                : Path.Combine(persistentDataPath, SettingsDirectoryName, SettingsFileName);
        }
        catch
        {
            return null;
        }
    }

    private static string GetLegacySettingsPath()
    {
        string modRoot = GetModRoot();
        return string.IsNullOrEmpty(modRoot) ? null : Path.Combine(modRoot, SettingsFileName);
    }

    private static string GetModRoot()
    {
        try
        {
            string assemblyPath = typeof(ItemAdderWindow).Assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            DirectoryInfo pluginDir = Directory.GetParent(assemblyPath);
            DirectoryInfo modDir = pluginDir?.Parent;
            return modDir?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static string Decode(string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
    }

    private void RebuildFiltered()
    {
        _filtered.Clear();
        string keyword = (_search ?? string.Empty).Trim();

        foreach (ItemEntry item in _items)
        {
            if (_selectedType >= 0 && item.ItemType != _selectedType)
            {
                continue;
            }

            if (_selectedGrade >= 0 && item.Grade != _selectedGrade)
            {
                continue;
            }

            if (!_showBlocked && item.Blocked)
            {
                continue;
            }

            if (keyword.Length > 0 &&
                item.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                item.TemplateId.ToString().IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            _filtered.Add(item);
        }

        _scroll.y = Mathf.Max(0f, _scroll.y);
    }

    private void StepAmount(int delta)
    {
        int current = ParseAmountOrDefault();
        SetAmount(current + delta);
    }

    private int ParseAmountOrDefault()
    {
        return int.TryParse(_amountText, out int amount) && amount > 0 ? amount : QuantityStep;
    }

    private void SetAmount(int amount)
    {
        amount = Math.Max(1, Math.Min(amount, 9999));
        _amountText = amount.ToString();
    }

    private void AddSelected()
    {
        ItemEntry item = _selectedItem;
        if (item == null)
        {
            return;
        }

        if (item.Blocked)
        {
            _status = $"已阻止添加：{item.Name}，原因：{item.BlockReason}";
            return;
        }

        if (!int.TryParse(_amountText, out int amount) || amount <= 0)
        {
            _status = "数量必须是正整数。";
            SetAmount(QuantityStep);
            return;
        }

        amount = Math.Min(amount, 9999);
        SetAmount(amount);

        try
        {
            int taiwuCharId = SingletonObject.getInstance<BasicGameData>().TaiwuCharId;
            if (taiwuCharId <= 0)
            {
                _status = "当前还没有可用的太吾角色 ID，请进入存档后再添加。";
                ModLogger.Warn("添加物品被阻止：当前太吾角色 ID 无效。");
                return;
            }

            if (!ItemTemplateHelper.CheckTemplateValid(item.ItemType, item.TemplateId))
            {
                _status = $"物品模板无效：type={item.ItemType}, id={item.TemplateId}";
                ModLogger.Warn($"添加物品被阻止：模板无效。type={item.ItemType}, id={item.TemplateId}, name={item.Name}");
                return;
            }

            GMFunc.GetItem(taiwuCharId, amount, item.ItemType, item.TemplateId, null);
            _status = $"已添加：{item.Name} x{amount}";
        }
        catch (Exception ex)
        {
            _status = "添加失败：" + ex.Message;
            ModLogger.Error($"添加物品失败。type={item.ItemType}, id={item.TemplateId}, name={item.Name}, amount={amount}", ex);
        }
    }

    private sealed class ItemEntry
    {
        public string Name;
        public sbyte ItemType;
        public short TemplateId;
        public sbyte Grade;
        public string Icon;
        public short ItemSubType;
        public short GroupId;
        public int BaseValue;
        public int BaseWeight;
        public short Duration;
        public sbyte MaxUseDistance;
        public bool Stackable;
        public bool Transferable;
        public string Description;
        public string FunctionDesc;
        public string TypeName;
        public bool Blocked;
        public string BlockReason;

        public static ItemEntry FromConfig(IItemConfig config)
        {
            string reason = GetBlockReason(config);
            return new ItemEntry
            {
                Name = string.IsNullOrEmpty(config.Name) ? "(未命名)" : config.Name,
                ItemType = config.ItemType,
                TemplateId = config.TemplateId,
                Grade = config.Grade,
                Icon = GetIcon(config),
                ItemSubType = config.ItemSubType,
                GroupId = config.GroupId,
                BaseValue = config.BaseValue,
                BaseWeight = GetIntField(config, "BaseWeight"),
                Duration = config.Duration,
                MaxUseDistance = config.MaxUseDistance,
                Stackable = GetBoolField(config, "Stackable"),
                Transferable = GetBoolField(config, "Transferable"),
                Description = GetStringField(config, "Desc"),
                FunctionDesc = GetStringField(config, "FunctionDesc"),
                TypeName = config.ItemType >= 0 && config.ItemType < TypeNames.Length ? TypeNames[config.ItemType] : "未知",
                Blocked = reason.Length > 0,
                BlockReason = reason
            };
        }

        private static string GetIcon(IItemConfig config)
        {
            if (!string.IsNullOrEmpty(config.Icon))
            {
                return config.Icon;
            }

            try
            {
                return ItemTemplateHelper.GetIcon(config.ItemType, config.TemplateId) ?? string.Empty;
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"读取物品配置图标名失败：type={config.ItemType}, id={config.TemplateId}", ex);
                return string.Empty;
            }
        }

        private static string GetBlockReason(IItemConfig config)
        {
            if (config.TaskLock != null && config.TaskLock.Count > 0)
            {
                return "任务锁";
            }

            if (HasTrueField(config, "IsSpecial"))
            {
                return "特殊物品";
            }

            if (config.ItemType == 12)
            {
                if (ItemTemplateHelper.CheckIsHeavenlyTreeSeeds(config.ItemType, config.TemplateId) ||
                    ItemTemplateHelper.CheckIsSectMainStoryItemXuannvNotes(config.ItemType, config.TemplateId) ||
                    ItemTemplateHelper.CheckIsSectMainStoryItemWuxianWugFairy(config.ItemType, config.TemplateId) ||
                    ItemTemplateHelper.CheckIsSectMainStoryFulongChickenMap(config.ItemType, config.TemplateId) ||
                    ItemTemplateHelper.CheckIsSectMainStoryItemYuanshanRosary(config.ItemType, config.TemplateId) ||
                    ItemTemplateHelper.CheckIsSectMainStoryItemJieQingStars(config.ItemType, config.TemplateId) ||
                    ItemTemplateHelper.CheckIsDamageHugeSword(config.ItemType, config.TemplateId) ||
                    ItemTemplateHelper.IsThanksLetter(config.ItemType, config.TemplateId))
                {
                    return "剧情/特殊杂物";
                }
            }

            string name = config.Name ?? string.Empty;
            if (name.Contains("剧情") || name.Contains("任务") || name.Contains("信物") || name.Contains("残卷"))
            {
                return "疑似剧情物品";
            }

            return string.Empty;
        }

        private static bool HasTrueField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName);
            return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(target);
        }

        private static bool GetBoolField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName);
            return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(target);
        }

        private static int GetIntField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName);
            if (field == null)
            {
                return 0;
            }

            object value = field.GetValue(target);
            return value is int intValue ? intValue : 0;
        }

        private static string GetStringField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName);
            if (field == null || field.FieldType != typeof(string))
            {
                return string.Empty;
            }

            return field.GetValue(target) as string ?? string.Empty;
        }
    }

    private sealed class ModSettings
    {
        public KeyCode ToggleKey = KeyCode.Home;

        public static ModSettings CreateDefault()
        {
            return new ModSettings();
        }

        public static ModSettings Load()
        {
            ModSettings settings = CreateDefault();
            string path = GetSettingsPath();
            if (string.IsNullOrEmpty(path))
            {
                ModLogger.Warn("无法取得持久化设置目录，将使用默认热键。");
                return settings;
            }

            try
            {
                if (!File.Exists(path))
                {
                    string legacyPath = GetLegacySettingsPath();
                    if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
                    {
                        ReadFromFile(settings, legacyPath);
                    }

                    settings.Save();
                    return settings;
                }

                ReadFromFile(settings, path);
            }
            catch (Exception ex)
            {
                ModLogger.Warn("读取设置文件失败，将使用默认设置。", ex);
            }

            return settings;
        }

        public void Save()
        {
            string path = GetSettingsPath();
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("无法取得持久化设置目录。");
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("持久化设置目录无效。");
            }

            Directory.CreateDirectory(directory);

            string[] lines =
            {
                "# TaiwuJiaJiaJia settings",
                "# ToggleKey uses Unity KeyCode names, for example: Home, F8, BackQuote.",
                "# More user options can be added here in later versions.",
                $"ToggleKey={ToggleKey}"
            };
            File.WriteAllLines(path, lines);
        }

        private static void ReadFromFile(ModSettings settings, string path)
        {
            string[] lines = File.ReadAllLines(path);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (!key.Equals("ToggleKey", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Enum.TryParse(value, ignoreCase: true, out KeyCode parsed) && parsed != KeyCode.None)
                {
                    settings.ToggleKey = parsed;
                }
                else
                {
                    ModLogger.Warn($"配置文件中的 ToggleKey 无效，将使用默认热键。value={value}");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FrameWork.ModSystem;
using GameData.Domains.Mod;
using GameData.Serializer;
using GameData.Utilities;
using UnityEngine;
using UnityEngine.UI;

public sealed class ManagerWindow : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F10;
    private const string PartnerConfigPath = "Config/Partners.txt";

    private readonly List<PartnerRuntime> _partners = new List<PartnerRuntime>();
    private readonly List<PartnerDefinition> _knownPartners = new List<PartnerDefinition>();
    private readonly List<string> _logs = new List<string>();
    private Rect _windowRect = new Rect(80f, 80f, 860f, 560f);
    private Vector2 _scroll;
    private Vector2 _logScroll;
    private Vector2 _dragOffset;
    private GameObject _inputBlockerRoot;
    private bool _visible;
    private bool _draggingWindow;
    private bool _busy;
    private bool _confirmUninstall;
    private string _status = "F10 呼出/隐藏。请先刷新状态，再执行准备卸载。";

    private GUIStyle _windowStyle;
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _okStyle;
    private GUIStyle _warnStyle;
    private GUIStyle _dangerButtonStyle;
    private GUIStyle _normalButtonStyle;
    private string _modIdStr;

    public void Initialize(string modIdStr)
    {
        _modIdStr = modIdStr;
        LoadPartnerDefinitions();
    }

    private void Awake()
    {
        CreateInputBlocker();
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
        if (Input.GetKeyDown(ToggleKey))
        {
            ToggleWindow();
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        EnsureStyles();
        GUI.depth = -1000;
        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "太吾好伙伴Manager", _windowStyle);
    }

    private void ToggleWindow()
    {
        _visible = !_visible;
        SetInputBlockerVisible(_visible);
        if (_visible)
        {
            RefreshPartnerList();
            RefreshAllStatuses();
        }
    }

    private void DrawWindow(int id)
    {
        HandleWindowDrag();
        var content = new Rect(16f, 32f, _windowRect.width - 32f, _windowRect.height - 48f);
        GUI.Box(new Rect(8f, 26f, _windowRect.width - 16f, _windowRect.height - 36f), GUIContent.none, _panelStyle);

        GUI.Label(new Rect(content.x, content.y, content.width - 220f, 24f), "好伙伴状态与卸载安全检查", _titleStyle);
        GUI.Label(new Rect(content.x + content.width - 210f, content.y + 2f, 210f, 22f), "热键：F10", _mutedStyle);

        var y = content.y + 34f;
        GUI.Label(new Rect(content.x, y, content.width, 38f),
            "流程：刷新状态 -> 勾选确认 -> 执行准备卸载 -> 成功后保存存档、退出游戏，再禁用或移除对应角色 Mod。",
            _mutedStyle);
        y += 46f;

        GUI.enabled = !_busy;
        if (GUI.Button(new Rect(content.x, y, 120f, 28f), "刷新状态", _normalButtonStyle))
        {
            RefreshPartnerList();
            RefreshAllStatuses();
        }

        _confirmUninstall = GUI.Toggle(new Rect(content.x + 136f, y + 3f, 360f, 24f), _confirmUninstall, "我理解：准备卸载后需要保存并退出游戏。");
        GUI.enabled = true;

        GUI.Label(new Rect(content.x + 510f, y + 4f, content.width - 510f, 22f), _status, _mutedStyle);
        y += 38f;

        var leftRect = new Rect(content.x, y, content.width * 0.58f, content.height - (y - content.y));
        var rightRect = new Rect(leftRect.xMax + 12f, y, content.width - leftRect.width - 12f, leftRect.height);
        DrawPartnerList(leftRect);
        DrawLogPanel(rightRect);

        GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 28f));
    }

    private void DrawPartnerList(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, _panelStyle);
        var view = new Rect(0f, 0f, rect.width - 22f, Mathf.Max(rect.height, _partners.Count * 144f + 10f));
        _scroll = GUI.BeginScrollView(rect, _scroll, view);

        var y = 8f;
        foreach (var partner in _partners)
        {
            var card = new Rect(8f, y, view.width - 16f, 132f);
            GUI.Box(card, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(card.x + 12f, card.y + 8f, 180f, 24f), partner.Definition.DisplayName, _titleStyle);
            GUI.Label(new Rect(card.x + 200f, card.y + 11f, card.width - 212f, 22f), partner.ModIdStr ?? "未发现已加载 Mod", partner.Loaded ? _okStyle : _warnStyle);
            GUI.Label(new Rect(card.x + 12f, card.y + 36f, card.width - 24f, 22f), partner.Definition.Description, _mutedStyle);
            GUI.Label(new Rect(card.x + 12f, card.y + 62f, card.width - 24f, 22f), BuildStatusText(partner), partner.UninstallPrepared ? _okStyle : _mutedStyle);
            GUI.Label(new Rect(card.x + 12f, card.y + 86f, card.width - 24f, 22f), partner.LastMessage, partner.LastSuccess ? _okStyle : _warnStyle);

            GUI.enabled = !_busy && partner.Loaded;
            if (GUI.Button(new Rect(card.x + card.width - 232f, card.y + 98f, 104f, 26f), "刷新", _normalButtonStyle))
            {
                RefreshStatus(partner);
            }

            GUI.enabled = !_busy && partner.Loaded && _confirmUninstall && !partner.UninstallPrepared;
            if (GUI.Button(new Rect(card.x + card.width - 120f, card.y + 98f, 104f, 26f), "准备卸载", _dangerButtonStyle))
            {
                PrepareUninstall(partner);
            }

            GUI.enabled = true;
            y += 144f;
        }

        GUI.EndScrollView();
    }

    private void DrawLogPanel(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, _panelStyle);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 24f), "处理过程", _titleStyle);

        var logRect = new Rect(rect.x + 10f, rect.y + 40f, rect.width - 20f, rect.height - 50f);
        var view = new Rect(0f, 0f, logRect.width - 22f, Mathf.Max(logRect.height, _logs.Count * 22f + 8f));
        _logScroll = GUI.BeginScrollView(logRect, _logScroll, view);
        var y = 4f;
        foreach (var line in _logs)
        {
            GUI.Label(new Rect(4f, y, view.width - 8f, 20f), line, _mutedStyle);
            y += 22f;
        }

        GUI.EndScrollView();
    }

    private void RefreshPartnerList()
    {
        _partners.Clear();
        if (_knownPartners.Count == 0)
        {
            LoadPartnerDefinitions();
        }

        foreach (var definition in _knownPartners)
        {
            var runtime = new PartnerRuntime { Definition = definition };
            var discovery = FindPartnerMod(definition);
            runtime.DiscoveryDetails = discovery.Details;
            if (discovery.ModInfo != null)
            {
                runtime.Installed = true;
                runtime.ModTitle = discovery.ModInfo.Title;
                runtime.ModIdStr = discovery.CallModIdStr;
                runtime.Loaded = discovery.Loaded;
                runtime.LastMessage = discovery.Loaded
                    ? "已发现并加载：" + runtime.ModIdStr
                    : "已安装但未加载。";
            }
            else
            {
                runtime.Loaded = false;
                runtime.LastMessage = "未发现对应角色 Mod。";
            }

            _partners.Add(runtime);
            AddLog(definition.DisplayName + " 发现结果：" + runtime.LastMessage + " " + runtime.DiscoveryDetails);
        }

        AddLog("已刷新好伙伴列表，共 " + _knownPartners.Count + " 个配置。");
    }

    private void LoadPartnerDefinitions()
    {
        _knownPartners.Clear();
        var loadedFromConfig = false;
        var configPath = ResolveConfigPath();
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            try
            {
                foreach (var line in File.ReadAllLines(configPath))
                {
                    var definition = ParsePartnerDefinition(line);
                    if (definition == null)
                    {
                        continue;
                    }

                    _knownPartners.Add(definition);
                }

                loadedFromConfig = _knownPartners.Count > 0;
            }
            catch (Exception ex)
            {
                ManagerLogger.Warning("读取伙伴配置失败，使用内置默认配置。", ex);
            }
        }

        if (_knownPartners.Count == 0)
        {
            _knownPartners.Add(CreateAyaDefinition());
        }

        AddLog(loadedFromConfig ? "已读取伙伴配置：" + configPath : "使用内置伙伴配置。");
    }

    private string ResolveConfigPath()
    {
        try
        {
            if (!string.IsNullOrEmpty(_modIdStr))
            {
                var modInfo = ModManager.GetModInfo(_modIdStr);
                if (modInfo != null && !string.IsNullOrEmpty(modInfo.DirectoryName))
                {
                    return Path.Combine(modInfo.DirectoryName, PartnerConfigPath);
                }
            }

            var assemblyPath = typeof(ManagerWindow).Assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return string.Empty;
            }

            var pluginDir = Directory.GetParent(assemblyPath);
            var modRoot = pluginDir?.Parent;
            return modRoot == null ? string.Empty : Path.Combine(modRoot.FullName, PartnerConfigPath);
        }
        catch (Exception ex)
        {
            ManagerLogger.Warning("解析伙伴配置路径失败。", ex);
            return string.Empty;
        }
    }

    private static PartnerDefinition ParsePartnerDefinition(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = trimmed.Split('|');
        if (parts.Length < 4)
        {
            ManagerLogger.Warning("忽略无效伙伴配置行：" + line);
            return null;
        }

        var definition = new PartnerDefinition
        {
            DisplayName = parts[0].Trim(),
            ModTitle = parts[1].Trim(),
            BackendPlugin = parts[2].Trim(),
            MethodPrefix = parts[3].Trim(),
            Description = parts.Length >= 5 ? parts[4].Trim() : string.Empty
        };

        if (string.IsNullOrEmpty(definition.DisplayName)
            || string.IsNullOrEmpty(definition.ModTitle)
            || string.IsNullOrEmpty(definition.BackendPlugin)
            || string.IsNullOrEmpty(definition.MethodPrefix))
        {
            ManagerLogger.Warning("忽略字段不完整的伙伴配置行：" + line);
            return null;
        }

        return definition;
    }

    private static PartnerDefinition CreateAyaDefinition()
    {
        return new PartnerDefinition
        {
            DisplayName = "阿雅",
            ModTitle = "太吾好伙伴阿雅",
            BackendPlugin = "TaiwuGoodPartnerAya.dll",
            MethodPrefix = "TaiwuGoodPartnerAya.",
            Description = "净化玄灰的特殊伙伴。准备卸载会让阿雅退出随行并从地图位置移除。"
        };
    }

    private static PartnerDiscovery FindPartnerMod(PartnerDefinition definition)
    {
        var discovery = new PartnerDiscovery();
        try
        {
            var localMods = ModManager.LocalMods;
            if (localMods == null)
            {
                discovery.Details = "LocalMods 不可用。";
                return discovery;
            }

            var loadedIds = ReadLoadedModIds();
            var enabledIds = ReadEnabledModIds();
            discovery.Details = "loaded=[" + string.Join(",", loadedIds) + "], enabled=[" + string.Join(",", enabledIds) + "]";

            foreach (var mod in localMods.Values)
            {
                if (mod == null)
                {
                    continue;
                }

                if (!IsPartnerMod(mod, definition))
                {
                    continue;
                }

                discovery.ModInfo = mod;

                var modIdStr = mod.ModId.ToString();
                var loadedId = ResolveLoadedModId(modIdStr, loadedIds);
                var enabled = enabledIds.Contains(modIdStr) || loadedId != null || SafeIsModEnabled(mod);
                discovery.Loaded = loadedId != null || enabled && loadedIds.Count == 0;
                discovery.CallModIdStr = loadedId ?? modIdStr;
                discovery.Details += ", matched=" + mod.Title + ", localModId=" + modIdStr + ", callModId=" + discovery.CallModIdStr + ", loaded=" + discovery.Loaded;
                return discovery;
            }

            discovery.Details += ", no local match for title=" + definition.ModTitle + ", backend=" + definition.BackendPlugin;
        }
        catch (Exception ex)
        {
            ManagerLogger.Warning("扫描已启用伙伴 Mod 失败。", ex);
            discovery.Details = "扫描失败：" + ex.Message;
        }

        return discovery;
    }

    private static bool IsPartnerMod(ModInfo mod, PartnerDefinition definition)
    {
        if (!string.IsNullOrEmpty(mod.Title) && mod.Title.Contains(definition.ModTitle))
        {
            return true;
        }

        return mod.BackendPlugins != null
            && mod.BackendPlugins.Any(plugin => string.Equals(plugin, definition.BackendPlugin, StringComparison.OrdinalIgnoreCase));
    }

    private static bool SafeIsModEnabled(ModInfo mod)
    {
        try
        {
            return mod != null && ModManager.IsModEnabled(mod.ModId);
        }
        catch
        {
            return false;
        }
    }

    private static HashSet<string> ReadEnabledModIds()
    {
        var result = new HashSet<string>();
        try
        {
            if (ModManager.EnabledMods == null)
            {
                return result;
            }

            foreach (var modId in ModManager.EnabledMods)
            {
                result.Add(modId.ToString());
            }
        }
        catch (Exception ex)
        {
            ManagerLogger.Warning("读取 EnabledMods 失败。", ex);
        }

        return result;
    }

    private static HashSet<string> ReadLoadedModIds()
    {
        var result = new HashSet<string>();
        try
        {
            var field = typeof(ModManager).GetField("_loadedMods", BindingFlags.Static | BindingFlags.NonPublic);
            var values = field?.GetValue(null) as System.Collections.IEnumerable;
            if (values == null)
            {
                return result;
            }

            foreach (var value in values)
            {
                if (value != null)
                {
                    result.Add(value.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            ManagerLogger.Warning("读取 LoadedMods 失败。", ex);
        }

        return result;
    }

    private static string ResolveLoadedModId(string localModId, HashSet<string> loadedIds)
    {
        if (loadedIds == null || loadedIds.Count == 0)
        {
            return null;
        }

        if (loadedIds.Contains(localModId))
        {
            return localModId;
        }

        var localFileId = GetFileIdPart(localModId);
        if (!string.IsNullOrEmpty(localFileId))
        {
            foreach (var loadedId in loadedIds)
            {
                if (string.Equals(GetFileIdPart(loadedId), localFileId, StringComparison.Ordinal))
                {
                    return loadedId;
                }
            }
        }

        return null;
    }

    private static string GetFileIdPart(string modId)
    {
        if (string.IsNullOrEmpty(modId))
        {
            return string.Empty;
        }

        var parts = modId.Split('_');
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }

    private void RefreshAllStatuses()
    {
        foreach (var partner in _partners)
        {
            if (partner.Loaded)
            {
                RefreshStatus(partner);
            }
        }
    }

    private void RefreshStatus(PartnerRuntime partner)
    {
        if (!partner.Loaded)
        {
            return;
        }

        _busy = true;
        _status = "正在读取 " + partner.Definition.DisplayName + " 状态...";
        AddLog("读取状态：" + partner.Definition.DisplayName);

        CallPartner(partner, "GetStatus", new SerializableModData(), result =>
        {
            _busy = false;
            ApplyStatus(partner, result);
            _status = "状态刷新完成。";
            AddLog("状态完成：" + partner.Definition.DisplayName + " -> " + partner.LastMessage);
        });
    }

    private void PrepareUninstall(PartnerRuntime partner)
    {
        if (!partner.Loaded || !_confirmUninstall)
        {
            return;
        }

        _busy = true;
        _status = "正在执行 " + partner.Definition.DisplayName + " 准备卸载...";
        AddLog("准备卸载：" + partner.Definition.DisplayName);

        var payload = new SerializableModData();
        payload.Set("source", "TaiwuGoodPartnerManager");
        CallPartner(partner, "PrepareUninstall", payload, result =>
        {
            _busy = false;
            ApplyStatus(partner, result);
            if (partner.LastSuccess)
            {
                partner.UninstallPrepared = GetBool(result, "uninstallPrepared", partner.UninstallPrepared);
                _status = "准备卸载完成。请保存存档、退出游戏，再禁用或移除对应角色 Mod。";
            }
            else
            {
                _status = "准备卸载未完成，请查看过程日志。";
            }

            AddLog("准备卸载结果：" + partner.Definition.DisplayName + " -> " + partner.LastMessage);
            RefreshStatus(partner);
        });
    }

    private void CallPartner(PartnerRuntime partner, string methodName, SerializableModData payload, Action<SerializableModData> callback)
    {
        try
        {
            var fullMethod = partner.Definition.MethodPrefix + methodName;
            ModDomainMethod.AsyncCall.CallModMethodWithParamAndRet(null, partner.ModIdStr, fullMethod, payload, (offset, pool) =>
            {
                SerializableModData result = null;
                try
                {
                    Serializer.Deserialize(pool, offset, ref result);
                    callback(result ?? new SerializableModData());
                }
                catch (Exception ex)
                {
                    _busy = false;
                    partner.LastSuccess = false;
                    partner.LastMessage = "解析返回数据失败：" + ex.Message;
                    AddLog(partner.LastMessage);
                    ManagerLogger.Warning("解析伙伴接口返回失败。", ex);
                }
            });
        }
        catch (Exception ex)
        {
            _busy = false;
            partner.LastSuccess = false;
            partner.LastMessage = "调用接口失败：" + ex.Message;
            _status = partner.LastMessage;
            AddLog(partner.LastMessage);
            ManagerLogger.Warning("调用伙伴接口失败。", ex);
        }
    }

    private static void ApplyStatus(PartnerRuntime partner, SerializableModData data)
    {
        partner.LastSuccess = GetBool(data, "success", false);
        partner.LastMessage = GetString(data, "message", partner.LastSuccess ? "接口调用成功。" : "接口调用失败。");
        partner.CharacterId = GetInt(data, "characterId", GetInt(data, "ayaCharId", partner.CharacterId));
        partner.Exists = GetBool(data, "exists", GetBool(data, "ayaExists", partner.Exists));
        partner.Joined = GetBool(data, "joined", partner.Joined);
        partner.Placed = GetBool(data, "placed", partner.Placed);
        partner.Dismissed = GetBool(data, "dismissed", partner.Dismissed);
        partner.UninstallPrepared = GetBool(data, "uninstallPrepared", partner.UninstallPrepared);
        partner.CooldownUntilDate = GetInt(data, "cooldownUntilDate", partner.CooldownUntilDate);
    }

    private static string BuildStatusText(PartnerRuntime partner)
    {
        if (!partner.Loaded)
        {
            return partner.Installed ? "状态：已安装但未加载" : "状态：未安装/未识别";
        }

        return $"角色ID：{partner.CharacterId}  存在：{YesNo(partner.Exists)}  随行：{YesNo(partner.Joined)}  地图放置：{YesNo(partner.Placed)}  已离去：{YesNo(partner.Dismissed)}  卸载准备：{YesNo(partner.UninstallPrepared)}";
    }

    private static string YesNo(bool value)
    {
        return value ? "是" : "否";
    }

    private static int GetInt(SerializableModData data, string key, int fallback)
    {
        return data != null && data.Get(key, out int value) ? value : fallback;
    }

    private static bool GetBool(SerializableModData data, string key, bool fallback)
    {
        return data != null && data.Get(key, out bool value) ? value : fallback;
    }

    private static string GetString(SerializableModData data, string key, string fallback)
    {
        return data != null && data.Get(key, out string value) ? value : fallback;
    }

    private void AddLog(string message)
    {
        _logs.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
        if (_logs.Count > 80)
        {
            _logs.RemoveAt(0);
        }
    }

    private void CreateInputBlocker()
    {
        try
        {
            _inputBlockerRoot = new GameObject("TaiwuGoodPartnerManager.InputBlocker");
            _inputBlockerRoot.transform.SetParent(transform, worldPositionStays: false);

            var canvas = _inputBlockerRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            _inputBlockerRoot.AddComponent<CanvasScaler>();
            _inputBlockerRoot.AddComponent<GraphicRaycaster>();

            var group = _inputBlockerRoot.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            var imageObject = new GameObject("RaycastTarget");
            imageObject.transform.SetParent(_inputBlockerRoot.transform, worldPositionStays: false);
            var rectTransform = imageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var image = imageObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            SetInputBlockerVisible(false);
        }
        catch (Exception ex)
        {
            ManagerLogger.Warning("创建输入遮罩失败。", ex);
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

    private void HandleWindowDrag()
    {
        var current = Event.current;
        if (current == null)
        {
            return;
        }

        var dragRect = new Rect(0f, 0f, _windowRect.width, 28f);
        if (current.type == EventType.MouseDown && current.button == 0 && dragRect.Contains(current.mousePosition))
        {
            _draggingWindow = true;
            _dragOffset = current.mousePosition;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && _draggingWindow)
        {
            var mouseScreen = GUIUtility.GUIToScreenPoint(current.mousePosition);
            _windowRect.x = Mathf.Clamp(mouseScreen.x - _dragOffset.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(mouseScreen.y - _dragOffset.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
            current.Use();
        }
        else if (current.type == EventType.MouseUp && _draggingWindow)
        {
            _draggingWindow = false;
            current.Use();
        }
    }

    private void EnsureStyles()
    {
        if (_windowStyle != null)
        {
            return;
        }

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            fontSize = 16,
            normal = { textColor = new Color(0.95f, 0.9f, 0.82f) },
            padding = new RectOffset(8, 8, 24, 8)
        };

        _panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(new Color(0.12f, 0.18f, 0.18f, 0.94f)) },
            padding = new RectOffset(8, 8, 8, 8)
        };

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.96f, 0.82f, 0.48f) }
        };

        _mutedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = new Color(0.78f, 0.82f, 0.78f) }
        };

        _okStyle = new GUIStyle(_mutedStyle)
        {
            normal = { textColor = new Color(0.54f, 0.92f, 0.74f) }
        };

        _warnStyle = new GUIStyle(_mutedStyle)
        {
            normal = { textColor = new Color(1f, 0.66f, 0.45f) }
        };

        _normalButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.22f, 0.46f, 0.42f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.28f, 0.58f, 0.52f)) }
        };

        _dangerButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.66f, 0.24f, 0.22f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.78f, 0.31f, 0.26f)) }
        };
    }

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private sealed class PartnerDefinition
    {
        public string DisplayName;
        public string ModTitle;
        public string BackendPlugin;
        public string MethodPrefix;
        public string Description;
    }

    private sealed class PartnerRuntime
    {
        public PartnerDefinition Definition;
        public bool Installed;
        public bool Loaded;
        public string ModIdStr;
        public string ModTitle;
        public string DiscoveryDetails;
        public int CharacterId = -1;
        public bool Exists;
        public bool Joined;
        public bool Placed;
        public bool Dismissed;
        public bool UninstallPrepared;
        public int CooldownUntilDate;
        public bool LastSuccess;
        public string LastMessage = "尚未读取状态。";
    }

    private sealed class PartnerDiscovery
    {
        public ModInfo ModInfo;
        public bool Loaded;
        public string CallModIdStr;
        public string Details = string.Empty;
    }
}

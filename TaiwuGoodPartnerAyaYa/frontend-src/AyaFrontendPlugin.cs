using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FrameWork.ExternalTexture;
using FrameWork.ModSystem;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace TaiwuGoodPartnerAya.Frontend;

[PluginConfig("TaiwuGoodPartnerAyaFrontend", "Shermer", "0.1.18")]
public sealed class AyaFrontendPlugin : TaiwuRemakePlugin
{
    private const string AvatarName = "NpcFace_taiwu_good_partner_aya";
    private const string LogPrefix = "[TaiwuGoodPartnerAyaFrontend] ";
    private static string _modIdStr = string.Empty;
    private static string _modRoot = string.Empty;
    private Harmony _harmony;

    public override void Initialize()
    {
        _modIdStr = ModIdStr;
        _modRoot = ResolveModRoot();
        LogModMappingSnapshot("before-register");
        AyaAvatarTextureFallback.Initialize(_modRoot);
        RegisterFixedAvatarTextures(_modRoot);
        _harmony = new Harmony("shermer.taiwu.goodpartner.aya.frontend");
        _harmony.PatchAll(typeof(AyaFrontendPlugin).Assembly);
        LogModMappingSnapshot("after-register");
    }

    public override void Dispose()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
    }

    private void RegisterFixedAvatarTextures(string modRoot)
    {
        try
        {
            if (string.IsNullOrEmpty(modRoot))
            {
                Debug.LogWarning(LogPrefix + "Cannot resolve mod root.");
                return;
            }

            var textureRoot = Path.Combine(modRoot, "ModResources", "Textures");
            if (!Directory.Exists(textureRoot))
            {
                Debug.LogWarning(LogPrefix + "Texture directory is missing: " + textureRoot);
                return;
            }

            var textureCenter = SingletonObject.getInstance<TextureCenter>();
            var groupKey = "ModTexture_" + ModIdStr;
            if (!textureCenter.TryGetTextureGroup(groupKey, out var textureGroup))
            {
                textureCenter.LoadTextureGroupFromPath<PathKeyTextureGroup>(groupKey, textureRoot);
                textureCenter.TryGetTextureGroup(groupKey, out textureGroup);
            }

            if (textureGroup == null)
            {
                Debug.LogWarning(LogPrefix + "Cannot create texture group: " + groupKey);
                return;
            }

            var registered = 0;
            registered += RegisterTexture(textureGroup, textureRoot, "BigFace");
            registered += RegisterTexture(textureGroup, textureRoot, "NormalFace");
            registered += RegisterTexture(textureGroup, textureRoot, "SmallFace");

            if (registered < 3)
            {
                Debug.LogWarning(LogPrefix + "Only registered " + registered + "/3 fixed avatar textures.");
            }
            else
            {
                Debug.Log(LogPrefix + "Registered fixed avatar textures for " + AvatarName + " from " + textureRoot + ".");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(LogPrefix + "Failed to register fixed avatar textures. " + ex);
        }
    }

    private string ResolveModRoot()
    {
        try
        {
            var modInfo = ModManager.GetModInfo(ModIdStr);
            if (modInfo != null && !string.IsNullOrEmpty(modInfo.DirectoryName) && Directory.Exists(modInfo.DirectoryName))
            {
                return modInfo.DirectoryName;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning(LogPrefix + "Cannot resolve mod root from ModManager: " + ex.Message);
        }

        var pluginDirectory = ResolvePluginDirectory();
        return string.IsNullOrEmpty(pluginDirectory)
            ? string.Empty
            : Path.GetFullPath(Path.Combine(pluginDirectory, ".."));
    }

    private static string ResolvePluginDirectory()
    {
        var assembly = typeof(AyaFrontendPlugin).Assembly;
        var candidates = new[]
        {
            assembly.Location,
            assembly.CodeBase
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            try
            {
                var path = candidate;
                if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && uri.IsFile)
                {
                    path = uri.LocalPath;
                }

                if (File.Exists(path))
                {
                    return Path.GetDirectoryName(path);
                }

                if (Directory.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "Skip invalid plugin path: " + ex.Message);
            }
        }

        return string.Empty;
    }

    private static int RegisterTexture(BaseTextureGroup textureGroup, string textureRoot, string sizeFolder)
    {
        var key = "NpcFace/" + sizeFolder + "/" + AvatarName;
        var path = Path.Combine(textureRoot, "NpcFace", sizeFolder, AvatarName + ".png");
        if (!File.Exists(path))
        {
            Debug.LogWarning(LogPrefix + "Missing texture file for key " + key + ": " + path);
            return 0;
        }

        textureGroup.AddTexture(key, path);
        return 1;
    }

    private static void LogModMappingSnapshot(string phase)
    {
        try
        {
            Debug.Log(LogPrefix + "Mod mapping snapshot [" + phase + "]. Self ModIdStr=" + _modIdStr + ", ModRoot=" + _modRoot);

            var selfInfo = ModManager.GetModInfo(_modIdStr);
            if (selfInfo == null)
            {
                Debug.LogWarning(LogPrefix + "ModManager.GetModInfo(Self ModIdStr) returned null. Self ModIdStr=" + _modIdStr);
            }
            else
            {
                Debug.Log(LogPrefix + "Self mod info: " + FormatModInfo(_modIdStr, selfInfo));
            }

            var localMods = ModManager.LocalMods;
            Debug.Log(LogPrefix + "LocalMods count=" + (localMods == null ? -1 : localMods.Count));
            if (localMods != null)
            {
                foreach (var pair in localMods)
                {
                    Debug.Log(LogPrefix + "LocalMod key=" + pair.Key + " => " + FormatModInfo(pair.Key, pair.Value));
                }
            }

            var enabledMods = ModManager.EnabledMods;
            Debug.Log(LogPrefix + "EnabledMods count=" + (enabledMods == null ? -1 : enabledMods.Count));
            if (enabledMods != null)
            {
                for (var i = 0; i < enabledMods.Count; i++)
                {
                    var modId = enabledMods[i];
                    Debug.Log(LogPrefix + "EnabledMod[" + i + "]=" + modId + ", modInfo=" + FormatMaybeModInfo(modId.ToString()));
                }
            }

            var loadedMods = ReadPrivateModIdList("_loadedMods");
            Debug.Log(LogPrefix + "LoadedMods count=" + (loadedMods == null ? -1 : loadedMods.Count));
            if (loadedMods != null)
            {
                for (var i = 0; i < loadedMods.Count; i++)
                {
                    var modIdText = loadedMods[i]?.ToString() ?? "<null>";
                    Debug.Log(LogPrefix + "LoadedMod[" + i + "]=" + modIdText + ", modInfo=" + FormatMaybeModInfo(modIdText));
                }
            }

            LogTextureGroupState("self", _modIdStr);
            if (localMods != null)
            {
                foreach (var pair in localMods)
                {
                    if (pair.Value != null && IsAyaDirectory(pair.Value.DirectoryName))
                    {
                        LogTextureGroupState("local-aya-key-" + pair.Key, pair.Value.ModId.ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning(LogPrefix + "Failed to log mod mapping snapshot: " + ex);
        }
    }

    private static List<object> ReadPrivateModIdList(string fieldName)
    {
        var field = typeof(ModManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        var value = field?.GetValue(null) as System.Collections.IEnumerable;
        if (value == null)
        {
            return null;
        }

        var result = new List<object>();
        foreach (var item in value)
        {
            result.Add(item);
        }

        return result;
    }

    private static string FormatMaybeModInfo(string modIdText)
    {
        try
        {
            var modInfo = ModManager.GetModInfo(modIdText);
            return modInfo == null ? "<null>" : FormatModInfo(modIdText, modInfo);
        }
        catch (Exception ex)
        {
            return "<error " + ex.Message + ">";
        }
    }

    private static string FormatModInfo(string dictionaryKey, ModInfoWithDisplayData modInfo)
    {
        if (modInfo == null)
        {
            return "<null>";
        }

        return "dictKey=" + dictionaryKey
            + ", modId=" + modInfo.ModId
            + ", title=" + modInfo.Title
            + ", dir=" + modInfo.DirectoryName
            + ", frontend=[" + JoinList(modInfo.FrontendPlugins) + "]"
            + ", backend=[" + JoinList(modInfo.BackendPlugins) + "]";
    }

    private static string JoinList(IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(",", values);
    }

    private static bool IsAyaDirectory(string directoryName)
    {
        if (string.IsNullOrEmpty(directoryName))
        {
            return false;
        }

        return string.Equals(Path.GetFileName(directoryName), "TaiwuGoodPartnerAya", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogTextureGroupState(string label, string modIdText)
    {
        try
        {
            var textureCenter = SingletonObject.getInstance<TextureCenter>();
            var groupKey = "ModTexture_" + modIdText;
            var hasGroup = textureCenter.TryGetTextureGroup(groupKey, out var textureGroup);
            Debug.Log(LogPrefix + "TextureGroup[" + label + "] key=" + groupKey + ", exists=" + hasGroup);
            if (!hasGroup || textureGroup == null)
            {
                return;
            }

            LogTextureProbe(textureGroup, "BigFace");
            LogTextureProbe(textureGroup, "NormalFace");
            LogTextureProbe(textureGroup, "SmallFace");
        }
        catch (Exception ex)
        {
            Debug.LogWarning(LogPrefix + "Failed to log texture group state: " + ex.Message);
        }
    }

    private static void LogTextureProbe(BaseTextureGroup textureGroup, string sizeFolder)
    {
        var key = "NpcFace/" + sizeFolder + "/" + AvatarName;
        var texture = textureGroup.GetTexture(key);
        Debug.Log(LogPrefix + "Texture probe key=" + key + ", hit=" + (texture != null));
    }

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.GetOverwriteTexture))]
    private static class GetOverwriteTexturePatch
    {
        private static readonly HashSet<string> LoggedPaths = new HashSet<string>();

        private static void Postfix(string path, ref Texture2D __result)
        {
            if (__result != null)
            {
                LogAvatarTextureRequest(path, officialHit: true, fallbackHit: false);
                return;
            }

            var fallback = AyaAvatarTextureFallback.TryGetTexture(path);
            __result = fallback;
            LogAvatarTextureRequest(path, officialHit: false, fallbackHit: fallback != null);
        }

        private static void LogAvatarTextureRequest(string path, bool officialHit, bool fallbackHit)
        {
            if (!AyaAvatarTextureFallback.IsAyaTexturePath(path) || !LoggedPaths.Add(path))
            {
                return;
            }

            Debug.Log(LogPrefix + "Avatar texture request path=" + path
                + ", officialHit=" + officialHit
                + ", fallbackHit=" + fallbackHit
                + ", selfModInfo=" + FormatMaybeModInfo(_modIdStr));
            LogTextureGroupState("request-self", _modIdStr);
        }
    }

    private static class AyaAvatarTextureFallback
    {
        private static readonly Dictionary<string, string> TexturePaths = new Dictionary<string, string>();
        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        public static void Initialize(string modRoot)
        {
            TexturePaths.Clear();
            TextureCache.Clear();

            if (string.IsNullOrEmpty(modRoot))
            {
                return;
            }

            var textureRoot = Path.Combine(modRoot, "ModResources", "Textures", "NpcFace");
            AddPath(textureRoot, "BigFace");
            AddPath(textureRoot, "NormalFace");
            AddPath(textureRoot, "SmallFace");
        }

        public static Texture2D TryGetTexture(string path)
        {
            if (string.IsNullOrEmpty(path) || !TexturePaths.TryGetValue(path, out var filePath))
            {
                return null;
            }

            if (TextureCache.TryGetValue(path, out var cached) && cached != null)
            {
                return cached;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogWarning(LogPrefix + "Fallback texture file is missing: " + filePath);
                return null;
            }

            try
            {
                var bytes = File.ReadAllBytes(filePath);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texture.name = Path.GetFileNameWithoutExtension(filePath);
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    Debug.LogWarning(LogPrefix + "Fallback texture decode failed: " + filePath);
                    return null;
                }

                TextureCache[path] = texture;
                Debug.Log(LogPrefix + "Loaded fallback avatar texture: " + path);
                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "Fallback texture load failed: " + ex.Message);
                return null;
            }
        }

        public static bool IsAyaTexturePath(string path)
        {
            return !string.IsNullOrEmpty(path) && TexturePaths.ContainsKey(path);
        }

        private static void AddPath(string textureRoot, string sizeFolder)
        {
            var key = "NpcFace/" + sizeFolder + "/" + AvatarName;
            var filePath = Path.Combine(textureRoot, sizeFolder, AvatarName + ".png");
            TexturePaths[key] = filePath;
        }
    }
}

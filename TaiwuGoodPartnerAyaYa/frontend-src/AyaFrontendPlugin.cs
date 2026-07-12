using System;
using System.IO;
using FrameWork.ExternalTexture;
using FrameWork.ModSystem;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace TaiwuGoodPartnerAya.Frontend;

[PluginConfig("TaiwuGoodPartnerAyaFrontend", "Shermer", "0.1.17")]
public sealed class AyaFrontendPlugin : TaiwuRemakePlugin
{
    private const string AvatarName = "NpcFace_taiwu_good_partner_aya";
    private const string LogPrefix = "[TaiwuGoodPartnerAyaFrontend] ";

    public override void Initialize()
    {
        RegisterFixedAvatarTextures();
    }

    public override void Dispose()
    {
    }

    private void RegisterFixedAvatarTextures()
    {
        try
        {
            var modRoot = ResolveModRoot();
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
}

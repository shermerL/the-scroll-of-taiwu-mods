using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CharacterConfig = Config.Character;
using GameData;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.AvatarSystem;
using GameData.Domains.Map;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.DomainEvents;

namespace TaiwuGoodPartnerAya;

internal sealed class AyaService
{
    public const string IntroEventGuid = "7f1a3a75-2e6a-4bb3-b92e-8ab9636e8c01";

    private const string ArchiveKeyPrefix = "TaiwuGoodPartnerAya.";
    private const string AyaCharacterIdKey = ArchiveKeyPrefix + "CharacterId";
    private const string AyaJoinedKey = ArchiveKeyPrefix + "Joined";
    private const string AyaIntroTriggeredKey = ArchiveKeyPrefix + "IntroTriggered";
    private const string AyaPlacedKey = ArchiveKeyPrefix + "Placed";
    private const string AyaSpawnDateKey = ArchiveKeyPrefix + "SpawnDate";
    private const string AyaDismissedKey = ArchiveKeyPrefix + "Dismissed";
    private const string UninstallPreparedKey = ArchiveKeyPrefix + "UninstallPrepared";
    private const string CooldownUntilDateKey = ArchiveKeyPrefix + "PurifyCooldownUntilDate";
    private const string LegacyGearMateCleanedKey = ArchiveKeyPrefix + "LegacyGearMateCleaned";
    private const int FixedCharacterTemplateId = 12001;
    private const int AutoLeaveMonths = 6;

    private readonly string _modIdStr;
    private readonly AyaConfig _config;

    public AyaService(string modIdStr, AyaConfig config)
    {
        _modIdStr = modIdStr;
        _config = config;
    }

    public void EnsureAvatarDataInstalled(string pluginDirectory)
    {
        try
        {
            var relativePath = NormalizeAvatarDataPath(_config.AssetPaths?.AvatarData);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var sourcePath = FindAvatarDataSource(pluginDirectory, relativePath);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                ModLogger.Warning("未找到阿雅 AvatarData 源文件：" + relativePath);
                return;
            }

            var dataPath = GetGameDataPath(pluginDirectory);
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                ModLogger.Warning("无法取得游戏 DataPath，暂不能安装阿雅 AvatarData。");
                return;
            }

            var destinationPath = Path.Combine(dataPath, "StreamingAssets", "CharacterAvatarData", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);

            if (!File.Exists(destinationPath) || File.ReadAllText(destinationPath) != File.ReadAllText(sourcePath))
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
                AvatarDataLoader.ClearCache();
                ModLogger.Diagnostic("EnsureAvatarDataInstalled: copied " + relativePath);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warning("安装阿雅 AvatarData 失败：" + ex.Message);
        }
    }

    public SerializableModData EnsureAya(DataContext context, SerializableModData data)
    {
        if (GetBool(UninstallPreparedKey, false))
        {
            return MakeResult(false, "uninstallPrepared", "阿雅已经告别，不会再自动出现。", 0);
        }

        if (GetBool(AyaDismissedKey, false))
        {
            return MakeResult(false, "dismissed", "阿雅已经离开太吾村，去寻找哥哥了。", 0);
        }

        if (!_config.IntroEnabled)
        {
            return MakeResult(false, "introDisabled", "阿雅初遇已在配置中关闭。", 0);
        }

        var templateId = FixedCharacterTemplateId;

        Character aya = null;
        try
        {
            var item = CharacterConfig.Instance[(short)templateId];
            if (item == null || item.CreatingType != 0)
            {
                return MakeResult(false, "invalidTemplate", "阿雅模板必须是固定人物模板。", 0);
            }

            if (!DomainManager.Character.TryGetFixedCharacterByTemplateId((short)templateId, out aya))
            {
                aya = DomainManager.Character.CreateFixedCharacter(context, (short)templateId);
                DomainManager.Character.CompleteCreatingCharacter(aya.GetId());
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error("创建或读取阿雅固定人物失败。", ex);
            return MakeResult(false, "createFailed", ex.Message, 0);
        }

        if (aya == null)
        {
            return MakeResult(false, "createFailed", "阿雅固定人物创建失败。", 0);
        }

        TryApplyAyaAvatar(context, aya);

        var moveToTaiwu = GetBool(data, "moveToTaiwu", true);
        if (moveToTaiwu)
        {
            MoveAyaToTaiwu(context, aya);
        }

        SetInt(context, AyaCharacterIdKey, aya.GetId());
        SetBool(context, AyaPlacedKey, true);
        if (GetInt(AyaSpawnDateKey, 0) <= 0)
        {
            SetInt(context, AyaSpawnDateKey, DomainManager.World.GetCurrDate());
        }

        var result = MakeResult(true, "ok", "阿雅已经准备好。", 0);
        result.Set("ayaCharId", aya.GetId());
        result.Set("fixedCharacterTemplateId", templateId);
        result.Set("introTriggered", GetBool(AyaIntroTriggeredKey, false));
        return result;
    }

    public void TryAutoPlaceAya(DataContext context)
    {
        try
        {
            if (context == null)
            {
                ModLogger.Diagnostic("TryAutoPlaceAya: skip. reason=contextNull");
                return;
            }

            if (IsAutoPlaceDisabled(out var disabledReason))
            {
                ModLogger.Diagnostic("TryAutoPlaceAya: skip. reason=" + disabledReason);
                return;
            }

            var taiwu = DomainManager.Taiwu.GetTaiwu();
            if (taiwu == null || !taiwu.GetLocation().IsValid())
            {
                ModLogger.Diagnostic("TryAutoPlaceAya: skip. reason=taiwuLocationInvalid");
                return;
            }

            if (GetBool(AyaJoinedKey, false))
            {
                SyncFollowerToTaiwu(context, "autoPlaceJoined");
                return;
            }

            var spawnDate = GetInt(AyaSpawnDateKey, 0);
            if (spawnDate > 0 && DomainManager.World.GetCurrDate() - spawnDate >= AutoLeaveMonths)
            {
                ModLogger.Diagnostic("TryAutoPlaceAya: dismiss Aya after auto leave months. spawnDate=" + spawnDate + ", currDate=" + DomainManager.World.GetCurrDate());
                DismissAya(context);
                return;
            }

            var data = new SerializableModData();
            data.Set("moveToTaiwu", true);
            var result = EnsureAya(context, data);
            if (!GetBool(result, "success", false))
            {
                ModLogger.Diagnostic("TryAutoPlaceAya: Ensure failed. code=" + GetResultString(result, "code", string.Empty) + ", message=" + GetResultString(result, "message", string.Empty));
                return;
            }

            SetBool(context, AyaPlacedKey, true);
            ModLogger.Diagnostic("TryAutoPlaceAya: Aya placed. charId=" + GetResultInt(result, "ayaCharId", -1));
        }
        catch (Exception ex)
        {
            ModLogger.Warning("自动放置阿雅时游戏状态尚未就绪，已跳过本次检查：" + ex.Message);
        }
    }

    public SerializableModData TriggerIntro(DataContext context, SerializableModData data)
    {
        if (GetBool(UninstallPreparedKey, false))
        {
            return MakeResult(false, "uninstallPrepared", "阿雅已经告别，不会再触发初遇。", 0);
        }

        if (GetBool(AyaIntroTriggeredKey, false))
        {
            return MakeResult(false, "alreadyTriggered", "阿雅初遇已经触发过。", 0);
        }

        var ensure = EnsureAya(context, data);
        if (!GetBool(ensure, "success", false))
        {
            return ensure;
        }

        return MakeResult(false, "manualOnly", "阿雅已在当前地块，请点击阿雅交谈。", 0);
    }

    public void RegisterAya(DataContext context, SerializableModData data)
    {
        if (GetBool(UninstallPreparedKey, false))
        {
            ModLogger.Warning("阿雅已准备卸载，忽略 RegisterAya。");
            return;
        }

        if (!TryGetInt(data, "charId", out var charId) || charId <= 0)
        {
            ModLogger.Warning("RegisterAya 缺少 charId。");
            return;
        }

        if (!DomainManager.Character.TryGetElement_Objects(charId, out var aya))
        {
            ModLogger.Warning("RegisterAya 找不到角色：" + charId);
            return;
        }

        if (!IsAyaFixedTemplate(aya))
        {
            ModLogger.Warning("RegisterAya 拒绝非阿雅固定模板角色。charId=" + charId);
            return;
        }

        SetInt(context, AyaCharacterIdKey, charId);
        SetBool(context, AyaIntroTriggeredKey, true);
    }

    public void JoinAya(DataContext context, SerializableModData data)
    {
        if (GetBool(UninstallPreparedKey, false))
        {
            ModLogger.Warning("阿雅已准备卸载，忽略 JoinAya。");
            return;
        }

        if (GetBool(AyaDismissedKey, false))
        {
            ModLogger.Warning("阿雅已经离去，忽略 JoinAya。");
            return;
        }

        if (!TryResolveAya(data, out var charId, out var aya))
        {
            return;
        }

        MoveAyaToTaiwu(context, aya);
        SetBool(context, LegacyGearMateCleanedKey, true);
        SetBool(context, AyaJoinedKey, true);
        SetBool(context, AyaIntroTriggeredKey, true);
        SetBool(context, AyaPlacedKey, true);
    }

    public void LeaveAya(DataContext context, SerializableModData data)
    {
        if (!TryResolveAya(data, out var charId, out _))
        {
            return;
        }

        TryCleanLegacyGearMate(context, charId, "leave");
        SetBool(context, AyaJoinedKey, false);
    }

    public void SyncFollowerToTaiwu(DataContext context, string reason)
    {
        if (!GetBool(AyaJoinedKey, false)
            || GetBool(UninstallPreparedKey, false)
            || GetBool(AyaDismissedKey, false))
        {
            return;
        }

        if (!TryResolveAya(new SerializableModData(), out var charId, out var aya))
        {
            return;
        }

        TryCleanLegacyGearMate(context, charId, reason);
        MoveAyaToTaiwu(context, aya);
        ModLogger.Diagnostic("SyncFollowerToTaiwu: moved Aya. reason=" + reason);
    }

    private void TryCleanLegacyGearMate(DataContext context, int charId, string reason)
    {
        if (GetBool(LegacyGearMateCleanedKey, false))
        {
            return;
        }

        try
        {
            DomainManager.Extra.GearMateLeaveGroup(context, charId);
        }
        catch (Exception ex)
        {
            ModLogger.Warning("清理旧版 GearMate 同道状态失败或无需清理。reason=" + reason + ", message=" + ex.Message);
        }

        SetBool(context, LegacyGearMateCleanedKey, true);
    }

    public SerializableModData PurifyCharacter(DataContext context, SerializableModData data)
    {
        if (GetBool(UninstallPreparedKey, false))
        {
            return MakeResult(false, "uninstallPrepared", "阿雅已经告别，无法再使用净化。", 0);
        }

        if (!TryGetInt(data, "targetCharId", out var targetCharId))
        {
            return MakeResult(false, "missingTarget", "没有指定需要净化的人。", 0);
        }

        if (!CanUsePurify(out var cooldownUntil))
        {
            return MakeResult(false, "cooldown", "阿雅刚刚用尽心力，还需要休息。", 0, cooldownUntil);
        }

        var taiwuLocation = GetTaiwuLocation();
        if (!taiwuLocation.IsValid())
        {
            return MakeResult(false, "invalidLocation", "太吾当前位置无效。", 0);
        }

        var purified = PurifyOne(context, targetCharId, taiwuLocation, out var message, out _);
        if (purified)
        {
            StartCooldown(context);
        }

        return MakeResult(purified, purified ? "ok" : "noTarget", message, purified ? 1 : 0, GetCooldownUntilDate());
    }

    public SerializableModData PurifyCurrentBlock(DataContext context, SerializableModData data)
    {
        if (GetBool(UninstallPreparedKey, false))
        {
            return MakeResult(false, "uninstallPrepared", "阿雅已经告别，无法再使用净化。", 0);
        }

        if (!CanUsePurify(out var cooldownUntil))
        {
            return MakeResult(false, "cooldown", "阿雅刚刚用尽心力，还需要休息。", 0, cooldownUntil);
        }

        var location = GetTaiwuLocation();
        if (!location.IsValid())
        {
            return MakeResult(false, "invalidLocation", "太吾当前位置无效。", 0);
        }

        var blockData = DomainManager.Map.GetBlockData(location.AreaId, location.BlockId);
        var candidates = CollectBlockCharacters(blockData).Distinct().ToList();
        var purifiedCount = 0;
        var skippedCount = 0;
        var purifiedNames = new List<string>();

        foreach (var charId in candidates)
        {
            if (PurifyOne(context, charId, location, out _, out var name))
            {
                purifiedCount++;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    purifiedNames.Add(name);
                }
            }
            else
            {
                skippedCount++;
            }
        }

        if (purifiedCount <= 0)
        {
            var result = MakeResult(false, "noTarget", "这里没有需要净化的人。", 0);
            result.Set("checkedCount", candidates.Count);
            result.Set("skippedCount", skippedCount);
            return result;
        }

        StartCooldown(context);
        var message = purifiedNames.Count > 0
            ? "阿雅轻轻合掌，净化了" + string.Join("、", purifiedNames) + "。"
            : "阿雅轻轻合掌，玄灰之气渐渐散去。";
        var ok = MakeResult(true, "ok", message, purifiedCount, GetCooldownUntilDate());
        ok.Set("checkedCount", candidates.Count);
        ok.Set("skippedCount", skippedCount);
        ok.Set("purifiedNames", string.Join("、", purifiedNames));
        return ok;
    }

    public SerializableModData PrepareUninstall(DataContext context, SerializableModData data)
    {
        if (IsRuntimeBusy())
        {
            return MakeResult(false, "busy", "当前处于战斗、奇遇或月结流程中，暂不可准备卸载。", 0);
        }

        if (TryResolveAya(data, out var charId, out _))
        {
            try
            {
                DomainManager.Extra.GearMateLeaveGroup(context, charId);
            }
            catch (Exception ex)
            {
                ModLogger.Warning("准备卸载时尝试让阿雅离队失败：" + ex.Message);
            }
        }

        SetBool(context, AyaJoinedKey, false);
        SetBool(context, AyaIntroTriggeredKey, true);
        SetBool(context, UninstallPreparedKey, true);

        var result = MakeResult(true, "uninstallPrepared", "阿雅已经暂别。保存后可关闭或移除此 Mod；重新安装后也不会自动再次触发初遇。", 0);
        result.Set("uninstallPrepared", true);
        return result;
    }

    public SerializableModData GetStatus(DataContext context)
    {
        var ayaCharId = GetInt(AyaCharacterIdKey, -1);
        var ayaExists = ayaCharId > 0 && DomainManager.Character.TryGetElement_Objects(ayaCharId, out var aya) && IsAyaFixedTemplate(aya);
        var uninstallPrepared = GetBool(UninstallPreparedKey, false);
        var result = new SerializableModData();
        result.Set("success", true);
        result.Set("ayaCharId", ayaExists ? ayaCharId : -1);
        result.Set("ayaExists", ayaExists);
        result.Set("joined", GetBool(AyaJoinedKey, false));
        result.Set("introTriggered", GetBool(AyaIntroTriggeredKey, false));
        result.Set("placed", GetBool(AyaPlacedKey, false));
        result.Set("dismissed", GetBool(AyaDismissedKey, false));
        result.Set("uninstallPrepared", uninstallPrepared);
        result.Set("cooldownUntilDate", GetCooldownUntilDate());
        result.Set("cooldownMonths", _config.PurifyCooldownMonths);
        result.Set("fixedCharacterTemplateId", FixedCharacterTemplateId);
        result.Set("spawnDate", GetInt(AyaSpawnDateKey, 0));
        result.Set("autoLeaveMonths", AutoLeaveMonths);
        result.Set("canPurify", !uninstallPrepared && CanUsePurify(out _));
        result.Set("currentBlockTargetCount", CountCurrentBlockPurifyTargets());
        return result;
    }

    private bool PurifyOne(DataContext context, int charId, Location taiwuLocation, out string message, out string targetName)
    {
        message = string.Empty;
        targetName = string.Empty;
        if (!DomainManager.Character.TryGetElement_Objects(charId, out var character))
        {
            message = "找不到目标。";
            return false;
        }

        if (!IsValidTarget(character, taiwuLocation, out message))
        {
            return false;
        }

        targetName = GetCharacterName(character);
        var hadXiangshu = character.GetXiangshuInfection() > 0;
        var hadDarkAsh = character.HasDarkAsh;
        if (!hadXiangshu && !hadDarkAsh)
        {
            message = "目标没有玄灰或相枢感染。";
            return false;
        }

        if (hadXiangshu)
        {
            DomainManager.Character.RemoveXiangshuInfection(context, character, 0);
            character.SavedFromInfected(context, batchMode: true);
        }

        if (hadDarkAsh)
        {
            character.RemoveDarkAsh(context);
        }

        message = "阿雅净化了" + targetName + "。";
        return true;
    }

    private bool IsValidTarget(Character character, Location taiwuLocation, out string reason)
    {
        reason = string.Empty;
        if (character.GetId() == DomainManager.Taiwu.GetTaiwuCharId())
        {
            reason = "太吾不作为首版净化目标。";
            return false;
        }

        var ayaCharId = GetInt(AyaCharacterIdKey, -1);
        if (ayaCharId > 0 && character.GetId() == ayaCharId)
        {
            reason = "阿雅不需要净化自己。";
            return false;
        }

        if (character.IsCreatedWithFixedTemplate())
        {
            reason = "首版不净化固定剧情人物。";
            return false;
        }

        if (IsCharacterDead(character))
        {
            reason = "目标已经死亡。";
            return false;
        }

        if (!character.GetAllowHeal())
        {
            reason = "目标不允许被治疗。";
            return false;
        }

        if (character.GetLocation() == Location.Invalid)
        {
            reason = "目标位置无效。";
            return false;
        }

        if (character.GetLocation() != taiwuLocation)
        {
            reason = "目标不在太吾当前地块。";
            return false;
        }

        return true;
    }

    private IEnumerable<int> CollectBlockCharacters(MapBlockData data)
    {
        if (data == null)
        {
            yield break;
        }

        if (data.InfectedCharacterSet != null)
        {
            foreach (var charId in data.InfectedCharacterSet)
            {
                yield return charId;
            }
        }

        if (data.CharacterSet != null)
        {
            foreach (var charId in data.CharacterSet)
            {
                yield return charId;
            }
        }

        if (data.FixedCharacterSet != null)
        {
            foreach (var charId in data.FixedCharacterSet)
            {
                yield return charId;
            }
        }
    }

    private bool CanUsePurify(out int cooldownUntil)
    {
        cooldownUntil = GetCooldownUntilDate();
        return cooldownUntil <= 0 || DomainManager.World.GetCurrDate() >= cooldownUntil;
    }

    private void StartCooldown(DataContext context)
    {
        var nextDate = DomainManager.World.GetCurrDate() + Math.Max(0, _config.PurifyCooldownMonths);
        SetInt(context, CooldownUntilDateKey, nextDate);
    }

    private int GetCooldownUntilDate()
    {
        return GetInt(CooldownUntilDateKey, 0);
    }

    private bool TryResolveAya(SerializableModData data, out int charId, out Character aya)
    {
        if (!TryGetInt(data, "charId", out charId))
        {
            charId = GetInt(AyaCharacterIdKey, -1);
        }

        if (charId <= 0)
        {
            aya = null;
            ModLogger.Warning("未注册阿雅角色，EventPackage 需要先调用 TaiwuGoodPartnerAya.Register。");
            return false;
        }

        if (!DomainManager.Character.TryGetElement_Objects(charId, out aya))
        {
            ModLogger.Warning("阿雅角色不存在：" + charId);
            return false;
        }

        TryApplyAyaAvatar(null, aya);

        return true;
    }

    private void TryApplyAyaAvatar(DataContext context, Character aya)
    {
        try
        {
            if (aya == null)
            {
                return;
            }

            var relativePath = NormalizeAvatarDataPath(_config.AssetPaths?.AvatarData);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var avatarData = AvatarDataLoader.Load(relativePath);
            if (avatarData == null)
            {
                ModLogger.Warning("阿雅 AvatarData 读取失败：" + relativePath);
                return;
            }

            var copy = new AvatarData(avatarData);
            var dataContext = context ?? DomainManager.TaiwuEvent.MainThreadDataContext;
            if (dataContext == null)
            {
                ModLogger.Warning("应用阿雅固定外观时 DataContext 尚未就绪。");
                return;
            }

            aya.SetAvatar(dataContext, copy);
        }
        catch (Exception ex)
        {
            ModLogger.Warning("应用阿雅固定外观失败：" + ex.Message);
        }
    }

    private static string NormalizeAvatarDataPath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath.Replace('\\', '/').TrimStart('/');
    }

    private static string FindAvatarDataSource(string pluginDirectory, string relativePath)
    {
        var candidates = new[]
        {
            Path.Combine(pluginDirectory, "..", "CharacterAvatarData", relativePath),
            Path.Combine(pluginDirectory, "CharacterAvatarData", relativePath)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string GetGameDataPath(string pluginDirectory)
    {
        try
        {
            var contextField = typeof(ExternalDataBridge).GetField("Context", BindingFlags.Static | BindingFlags.NonPublic);
            var context = contextField?.GetValue(null);
            var dataPathProperty = context?.GetType().GetProperty("DataPath", BindingFlags.Instance | BindingFlags.Public);
            var dataPath = dataPathProperty?.GetValue(context) as string;
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                return dataPath;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warning("反射读取游戏 DataPath 失败：" + ex.Message);
        }

        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(pluginDirectory, "..", "..", ".."),
            Path.Combine(pluginDirectory, "..", "..", "..", "..")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(Path.Combine(fullPath, "StreamingAssets")))
            {
                return fullPath;
            }
        }

        return string.Empty;
    }

    private int CountCurrentBlockPurifyTargets()
    {
        var location = GetTaiwuLocation();
        if (!location.IsValid())
        {
            return 0;
        }

        var blockData = DomainManager.Map.GetBlockData(location.AreaId, location.BlockId);
        var count = 0;
        foreach (var charId in CollectBlockCharacters(blockData).Distinct())
        {
            if (!DomainManager.Character.TryGetElement_Objects(charId, out var character))
            {
                continue;
            }

            if (!IsValidTarget(character, location, out _))
            {
                continue;
            }

            if (character.GetXiangshuInfection() > 0 || character.HasDarkAsh)
            {
                count++;
            }
        }

        return count;
    }

    private Location GetTaiwuLocation()
    {
        var taiwu = DomainManager.Taiwu.GetTaiwu();
        return taiwu == null ? Location.Invalid : taiwu.GetLocation();
    }

    private bool IsRuntimeBusy()
    {
        return DomainManager.Combat.IsInCombat()
            || DomainManager.Adventure.QueryTaiwuInAny()
            || DomainManager.World.GetAdvancingMonthState() != 0
            || DomainManager.TaiwuEvent.IsShowingEvent;
    }

    private bool IsAutoPlaceDisabled(out string reason)
    {
        if (GetBool(UninstallPreparedKey, false))
        {
            reason = "uninstallPrepared";
            return true;
        }

        if (GetBool(AyaDismissedKey, false))
        {
            reason = "dismissed";
            return true;
        }

        if (GetBool(AyaJoinedKey, false))
        {
            reason = "joined";
            return true;
        }

        if (GetBool(AyaIntroTriggeredKey, false))
        {
            reason = "introTriggered";
            return true;
        }

        if (!_config.IntroEnabled)
        {
            reason = "introDisabled";
            return true;
        }

        if (IsRuntimeBusy())
        {
            reason = "runtimeBusy";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private bool IsAyaFixedTemplate(Character character)
    {
        if (character == null || !character.IsCreatedWithFixedTemplate())
        {
            return false;
        }

        return DomainManager.Character.TryGetFixedCharacterByTemplateId((short)FixedCharacterTemplateId, out var aya)
            && aya != null
            && aya.GetId() == character.GetId();
    }

    private bool IsCharacterDead(Character character)
    {
        try
        {
            var method = character.GetType().GetMethod("GetDead", Type.EmptyTypes);
            if (method != null && method.ReturnType == typeof(bool))
            {
                return (bool)method.Invoke(character, null);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warning("读取角色死亡状态失败，已按未死亡处理：" + ex.Message);
        }

        return false;
    }

    private string GetCharacterName(Character character)
    {
        if (character == null)
        {
            return "此人";
        }

        try
        {
            var name = DomainManager.Character.GetName(character.GetId(), true);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warning("通过 CharacterDomain 读取角色姓名失败：" + ex.Message);
        }

        try
        {
            var realName = CharacterDomain.GetRealName(character);
            var combined = (realName.Item1 ?? string.Empty) + (realName.Item2 ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                return combined;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warning("读取角色真实姓名失败：" + ex.Message);
        }

        return "此人";
    }

    private SerializableModData MakeResult(bool success, string code, string message, int purifiedCount, int cooldownUntilDate = 0)
    {
        var result = new SerializableModData();
        result.Set("success", success);
        result.Set("code", code);
        result.Set("message", message);
        result.Set("purifiedCount", purifiedCount);
        result.Set("cooldownUntilDate", cooldownUntilDate);
        return result;
    }

    private bool TryGetInt(SerializableModData data, string key, out int value)
    {
        if (data != null && data.Get(key, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private bool GetBool(SerializableModData data, string key, bool fallback)
    {
        if (data != null && data.Get(key, out bool value))
        {
            return value;
        }

        return fallback;
    }

    private int GetResultInt(SerializableModData data, string key, int fallback)
    {
        if (data != null && data.Get(key, out int value))
        {
            return value;
        }

        return fallback;
    }

    private string GetResultString(SerializableModData data, string key, string fallback)
    {
        if (data != null && data.Get(key, out string value))
        {
            return value;
        }

        return fallback;
    }

    private int GetInt(string key, int fallback)
    {
        return DomainManager.Mod.TryGet(_modIdStr, key, true, out int value) ? value : fallback;
    }

    private bool GetBool(string key, bool fallback)
    {
        return DomainManager.Mod.TryGet(_modIdStr, key, true, out bool value) ? value : fallback;
    }

    private void SetInt(DataContext context, string key, int value)
    {
        DomainManager.Mod.SetInt(context, _modIdStr, key, true, value);
    }

    private void SetBool(DataContext context, string key, bool value)
    {
        DomainManager.Mod.SetBool(context, _modIdStr, key, true, value);
    }

    private void MoveAyaToTaiwu(DataContext context, Character aya)
    {
        var taiwu = DomainManager.Taiwu.GetTaiwu();
        if (taiwu == null)
        {
            ModLogger.Warning("无法移动阿雅：太吾不存在。");
            return;
        }

        var location = taiwu.GetLocation();
        if (!location.IsValid())
        {
            ModLogger.Warning("无法移动阿雅：太吾当前位置无效。");
            return;
        }

        var from = aya.GetLocation();
        if (from == location)
        {
            return;
        }

        Events.RaiseFixedCharacterLocationChanged(context, aya.GetId(), from, location);
        aya.SetLocation(location, context);
    }

    private void DismissAya(DataContext context)
    {
        if (TryResolveAya(null, out var charId, out var aya))
        {
            try
            {
                DomainManager.Extra.GearMateLeaveGroup(context, charId);
            }
            catch (Exception ex)
            {
                ModLogger.Warning("阿雅离去时尝试离队失败：" + ex.Message);
            }

            var from = aya.GetLocation();
            if (from != Location.Invalid)
            {
                Events.RaiseFixedCharacterLocationChanged(context, aya.GetId(), from, Location.Invalid);
                aya.SetLocation(Location.Invalid, context);
            }
        }

        SetBool(context, AyaJoinedKey, false);
        SetBool(context, AyaPlacedKey, false);
        SetBool(context, AyaDismissedKey, true);
        SetBool(context, AyaIntroTriggeredKey, true);
    }
}

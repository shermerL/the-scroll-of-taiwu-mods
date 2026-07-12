using System;
using System.IO;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Map;
using GameData.Domains.Mod;
using GameData.DomainEvents;
using TaiwuModdingLib.Core.Plugin;

namespace TaiwuGoodPartnerAya;

[PluginConfig("TaiwuGoodPartnerAya", "Shermer", "0.1.16")]
public sealed class AyaPlugin : TaiwuRemakePlugin
{
    private const string MethodPrefix = "TaiwuGoodPartnerAya.";

    private AyaConfig _config;
    private AyaService _service;

    public override void Initialize()
    {
        try
        {
            Reload();
            ModLogger.Diagnostic("Initialize: register backend methods. modId=" + ModIdStr);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "Ensure", EnsureAya);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "TriggerIntro", TriggerIntro);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "Register", RegisterAya);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "Join", JoinAya);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "Leave", LeaveAya);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "PrepareUninstall", PrepareUninstall);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "PurifyCharacter", PurifyCharacter);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "PurifyCurrentBlock", PurifyCurrentBlock);
            DomainManager.Mod.AddModMethod(ModIdStr, MethodPrefix + "GetStatus", GetStatus);
            Events.RegisterHandler_TaiwuMove(OnTaiwuMove);
            Events.RegisterHandler_AdvanceMonthFinish(OnAdvanceMonthFinish);
        }
        catch (Exception ex)
        {
            ModLogger.Error("初始化失败。", ex);
        }
    }

    public override void Dispose()
    {
        Events.UnRegisterHandler_TaiwuMove(OnTaiwuMove);
        Events.UnRegisterHandler_AdvanceMonthFinish(OnAdvanceMonthFinish);
        _service = null;
    }

    public override void OnModSettingUpdate()
    {
        Reload();
    }

    public override void OnLoadedArchiveData()
    {
        try
        {
            ModLogger.Diagnostic("OnLoadedArchiveData: try auto place Aya.");
            _service.TryAutoPlaceAya(DomainManager.TaiwuEvent.MainThreadDataContext);
        }
        catch (Exception ex)
        {
            ModLogger.Error("自动触发阿雅初遇失败。", ex);
        }
    }

    private void Reload()
    {
        var pluginDirectory = Path.GetDirectoryName(typeof(AyaPlugin).Assembly.Location) ?? string.Empty;
        _config = AyaConfig.Load(pluginDirectory);
        ModLogger.Initialize(pluginDirectory, _config.DiagnosticLogEnabled);
        ModLogger.Diagnostic("Reload: config loaded. introEnabled=" + _config.IntroEnabled + ", cooldown=" + _config.PurifyCooldownMonths);
        _service = new AyaService(ModIdStr, _config);
        _service.EnsureAvatarDataInstalled(pluginDirectory);
    }

    private void RegisterAya(DataContext context, SerializableModData data)
    {
        SafeInvoke(context, data, service => service.RegisterAya(context, data));
    }

    private SerializableModData EnsureAya(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.EnsureAya(context, data));
    }

    private SerializableModData TriggerIntro(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.TriggerIntro(context, data));
    }

    private SerializableModData JoinAya(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.JoinAya(context, data));
    }

    private SerializableModData LeaveAya(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.LeaveAya(context, data));
    }

    private void OnTaiwuMove(DataContext context, MapBlockData fromBlock, MapBlockData toBlock, int actionPointCost)
    {
        SafeInvoke(context, new SerializableModData(), service => service.SyncFollowerToTaiwu(context, "taiwuMove"));
    }

    private void OnAdvanceMonthFinish(DataContext context)
    {
        SafeInvoke(context, new SerializableModData(), service => service.SyncFollowerToTaiwu(context, "advanceMonthFinish"));
    }

    private SerializableModData PrepareUninstall(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.PrepareUninstall(context, data));
    }

    private SerializableModData PurifyCharacter(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.PurifyCharacter(context, data));
    }

    private SerializableModData PurifyCurrentBlock(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.PurifyCurrentBlock(context, data));
    }

    private SerializableModData GetStatus(DataContext context, SerializableModData data)
    {
        return SafeInvoke(context, data, service => service.GetStatus(context));
    }

    private void SafeInvoke(DataContext context, SerializableModData data, Action<AyaService> action)
    {
        try
        {
            action(_service);
        }
        catch (Exception ex)
        {
            ModLogger.Error("执行接口失败。", ex);
        }
    }

    private SerializableModData SafeInvoke(DataContext context, SerializableModData data, Func<AyaService, SerializableModData> action)
    {
        try
        {
            return action(_service);
        }
        catch (Exception ex)
        {
            ModLogger.Error("执行接口失败。", ex);
            var result = new SerializableModData();
            result.Set("success", false);
            result.Set("code", "error");
            result.Set("message", ex.Message);
            return result;
        }
    }
}

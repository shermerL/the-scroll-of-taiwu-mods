using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

[PluginConfig("TaiwuGoodPartnerManager", "Shermer", "0.1.2")]
public sealed class ManagerPlugin : TaiwuRemakePlugin
{
    private GameObject _host;

    public override void Initialize()
    {
        if (_host != null)
        {
            return;
        }

        try
        {
            _host = new GameObject("TaiwuGoodPartnerManager.Host");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<ManagerWindow>().Initialize(ModIdStr);
        }
        catch (System.Exception ex)
        {
            ManagerLogger.Error("Manager 初始化失败。", ex);
            throw;
        }
    }

    public override void Dispose()
    {
        if (_host == null)
        {
            return;
        }

        try
        {
            Object.Destroy(_host);
            _host = null;
        }
        catch (System.Exception ex)
        {
            ManagerLogger.Error("Manager 释放失败。", ex);
            throw;
        }
    }
}

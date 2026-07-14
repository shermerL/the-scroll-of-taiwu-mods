using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

[PluginConfig("TaiwuJiaJiaJia", "Shermer", "0.1.16")]
public sealed class ItemAdderPlugin : TaiwuRemakePlugin
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
            _host = new GameObject("TaiwuItemAdderFrontend.Host");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<ItemAdderWindow>();
        }
        catch (System.Exception ex)
        {
            ModLogger.Error("插件初始化失败。", ex);
            throw;
        }
    }

    public override void Dispose()
    {
        if (_host != null)
        {
            try
            {
                Object.Destroy(_host);
                _host = null;
            }
            catch (System.Exception ex)
            {
                ModLogger.Error("插件释放失败。", ex);
                throw;
            }
        }
    }
}

using System;
using System.IO;
using Newtonsoft.Json;

namespace TaiwuGoodPartnerAya;

internal sealed class AyaConfig
{
    public bool IntroEnabled { get; set; } = true;
    public bool DiagnosticLogEnabled { get; set; } = true;
    public int PurifyCooldownMonths { get; set; } = 6;
    public CombatSupportConfig CombatSupport { get; set; } = new CombatSupportConfig();
    public LoadoutConfig Loadout { get; set; } = new LoadoutConfig();
    public AssetPathConfig AssetPaths { get; set; } = new AssetPathConfig();

    public static AyaConfig Load(string pluginDirectory)
    {
        var config = new AyaConfig();
        var candidates = new[]
        {
            Path.Combine(pluginDirectory, "Config", "AyaConfig.json"),
            Path.Combine(pluginDirectory, "Config", "AyaYaConfig.json"),
            Path.Combine(pluginDirectory, "..", "Config", "AyaConfig.json"),
            Path.Combine(pluginDirectory, "..", "Config", "AyaYaConfig.json")
        };

        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var loaded = JsonConvert.DeserializeObject<AyaConfig>(File.ReadAllText(path));
                if (loaded == null)
                {
                    ModLogger.Warning("配置文件为空或格式不正确：" + path);
                    return config;
                }

                loaded.Normalize();
                return loaded;
            }
            catch (Exception ex)
            {
                ModLogger.Error("读取配置文件失败，已使用默认配置。", ex);
                return config;
            }
        }

        config.Normalize();
        return config;
    }

    public void Normalize()
    {
        if (PurifyCooldownMonths < 0)
        {
            ModLogger.Warning("purifyCooldownMonths 小于 0，已修正为 0。");
            PurifyCooldownMonths = 0;
        }

        CombatSupport ??= new CombatSupportConfig();
        Loadout ??= new LoadoutConfig();
        AssetPaths ??= new AssetPathConfig();
    }
}

internal sealed class CombatSupportConfig
{
    public bool Enabled { get; set; } = true;
    public int[] PresetTeammateCommandIds { get; set; } = { 7, 6 };
    public bool UseVanillaCosts { get; set; } = true;
}

internal sealed class LoadoutConfig
{
    public int[] WeaponTemplateIds { get; set; } = { 505 };
    public int[] ArmorTemplateIds { get; set; } = { 39 };
    public int[] ClothingTemplateIds { get; set; } = { 24 };
    public int[] AccessoryTemplateIds { get; set; } = { 38 };
    public MedicineConfig[] MedicineTemplateIds { get; set; } = { new MedicineConfig { TemplateId = 95, Count = 3 } };
}

internal sealed class MedicineConfig
{
    public int TemplateId { get; set; }
    public int Count { get; set; }
}

internal sealed class AssetPathConfig
{
    public string EventPortrait { get; set; } = "Assets/Images/ayaya-event-portrait-taiwu-style.png";
    public string AvatarData { get; set; } = "TaiwuGoodPartnerAya/Aya.txt";
    public string WorkshopCover { get; set; } = "workshop/WorkshopCover.jpg";
}

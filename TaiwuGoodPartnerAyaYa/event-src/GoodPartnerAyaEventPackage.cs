using System;
using System.Collections.Generic;
using Config;
using Config.EventConfig;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Mod;
using GameData.Domains.TaiwuEvent;
using GameData.Domains.TaiwuEvent.Enum;

namespace Taiwu_EventPackage_GoodPartnerAya;

public sealed class Taiwu_EventPackage_GoodPartnerAya : EventPackage
{
    internal const string IntroGuid = "7f1a3a75-2e6a-4bb3-b92e-8ab9636e8c01";
    internal const string InteractGuid = "6c3b91f2-92cf-4820-a50f-60dddb3c489d";
    internal const string ResultGuid = "9f239db1-0ea5-44d1-aa3b-882d3bdad11b";
    internal const string ModId = "TaiwuGoodPartnerAya";
    internal const string MethodPrefix = "TaiwuGoodPartnerAya.";
    internal const string RoleKey = "TaiwuGoodPartnerAya.Aya";
    internal const string ResultCodeKey = "TaiwuGoodPartnerAya.ResultCode";
    internal const string ResultMessageKey = "TaiwuGoodPartnerAya.ResultMessage";
    internal const string PurifiedCountKey = "TaiwuGoodPartnerAya.PurifiedCount";
    internal const string CooldownUntilDateKey = "TaiwuGoodPartnerAya.CooldownUntilDate";

    public Taiwu_EventPackage_GoodPartnerAya()
    {
        NameSpace = "TaiwuGoodPartner";
        Author = "Shermer";
        Group = "TaiwuGoodPartnerAya";
        EventList = new List<TaiwuEventItem>
        {
            new AyaIntroEvent(),
            new AyaInteractEvent(),
            new AyaResultEvent()
        };
    }
}

internal abstract class AyaEventBase : TaiwuEventItem
{
    protected AyaEventBase(string guid, short triggerType, bool isHeadEvent)
    {
        Guid = Guid.Parse(guid);
        EventGroup = "GoodPartnerAya";
        EventType = EEventType.ModEvent;
        TriggerType = triggerType;
        IsHeadEvent = isHeadEvent;
        EventSortingOrder = 30;
        MainRoleKey = EventArgBox.RoleTaiwu;
        TargetRoleKey = Taiwu_EventPackage_GoodPartnerAya.RoleKey;
        EventOptions = Array.Empty<TaiwuEventOption>();
    }

    public override void OnEventEnter()
    {
    }

    public override void OnEventExit()
    {
    }

    public override string GetReplacedContentString()
    {
        return EventContent;
    }

    protected SerializableModData Call(string method)
    {
        return DomainManager.Mod.CallModMethodWithParamAndRet(
            DomainManager.TaiwuEvent.MainThreadDataContext,
            GetModId(),
            method,
            new SerializableModData());
    }

    protected SerializableModData Call(string method, SerializableModData data)
    {
        return DomainManager.Mod.CallModMethodWithParamAndRet(DomainManager.TaiwuEvent.MainThreadDataContext, GetModId(), method, data);
    }

    protected bool TryCall(string method, out SerializableModData result)
    {
        try
        {
            result = Call(method);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    protected bool TryCall(string method, SerializableModData data, out SerializableModData result)
    {
        try
        {
            result = Call(method, data);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    protected void CallNoRet(string method, SerializableModData data)
    {
        DomainManager.Mod.CallModMethodWithParam(DomainManager.TaiwuEvent.MainThreadDataContext, GetModId(), method, data);
    }

    protected bool TryCallNoRet(string method, SerializableModData data)
    {
        try
        {
            CallNoRet(method, data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetModId()
    {
        return !string.IsNullOrEmpty(Package?.ModIdString)
            ? Package.ModIdString
            : Taiwu_EventPackage_GoodPartnerAya.ModId;
    }

    protected bool GetResultBool(SerializableModData data, string key, bool fallback)
    {
        if (data != null && data.Get(key, out bool value))
        {
            return value;
        }

        return fallback;
    }

    protected int GetResultInt(SerializableModData data, string key, int fallback)
    {
        if (data != null && data.Get(key, out int value))
        {
            return value;
        }

        return fallback;
    }

    protected string GetResultString(SerializableModData data, string key, string fallback)
    {
        if (data != null && data.Get(key, out string value))
        {
            return value;
        }

        return fallback;
    }

    protected bool BindAyaFromStatus()
    {
        if (ArgBox == null)
        {
            return false;
        }

        if (!TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "GetStatus", out var status))
        {
            return false;
        }

        var ayaCharId = GetResultInt(status, "ayaCharId", -1);
        if (ayaCharId <= 0)
        {
            return false;
        }

        ArgBox.Set(Taiwu_EventPackage_GoodPartnerAya.RoleKey, ayaCharId);
        return true;
    }

    protected void SetResult(SerializableModData result)
    {
        if (ArgBox == null)
        {
            return;
        }

        ArgBox.Set(Taiwu_EventPackage_GoodPartnerAya.ResultCodeKey, GetResultString(result, "code", string.Empty));
        ArgBox.Set(Taiwu_EventPackage_GoodPartnerAya.ResultMessageKey, GetResultString(result, "message", string.Empty));
        ArgBox.Set(Taiwu_EventPackage_GoodPartnerAya.PurifiedCountKey, GetResultInt(result, "purifiedCount", 0));
        ArgBox.Set(Taiwu_EventPackage_GoodPartnerAya.CooldownUntilDateKey, GetResultInt(result, "cooldownUntilDate", 0));
    }
}

internal sealed class AyaIntroEvent : AyaEventBase
{
    public AyaIntroEvent()
        : base(Taiwu_EventPackage_GoodPartnerAya.IntroGuid, 0, isHeadEvent: true)
    {
        EventOptions = new[]
        {
            new TaiwuEventOption
            {
                OptionKey = "stay",
                OptionGuid = "746ea097-9f6d-4ee6-9829-c4f4e9bf6291",
                OnOptionSelect = OnStay
            },
            new TaiwuEventOption
            {
                OptionKey = "later",
                OptionGuid = "a415a3c2-0585-4f62-a789-8f0141f3d425",
                OnOptionSelect = OnLater
            }
        };
    }

    public override bool OnCheckEventCondition()
    {
        if (ArgBox == null)
        {
            return false;
        }

        if (!CanTriggerIntroNow())
        {
            return false;
        }

        if (!TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "GetStatus", out var status))
        {
            return false;
        }

        if (GetResultBool(status, "introTriggered", false) || GetResultBool(status, "uninstallPrepared", false))
        {
            return false;
        }

        var data = new SerializableModData();
        data.Set("moveToTaiwu", true);
        if (!TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "Ensure", data, out var result))
        {
            return false;
        }

        if (!GetResultBool(result, "success", false))
        {
            return false;
        }

        var ayaCharId = GetResultInt(result, "ayaCharId", -1);
        if (ayaCharId <= 0)
        {
            return false;
        }

        ArgBox.Set(Taiwu_EventPackage_GoodPartnerAya.RoleKey, ayaCharId);
        return false;
    }

    private bool CanTriggerIntroNow()
    {
        try
        {
            var taiwu = DomainManager.Taiwu.GetTaiwu();
            if (taiwu == null || !taiwu.GetLocation().IsValid())
            {
                return false;
            }

            if (DomainManager.Combat.IsInCombat() || DomainManager.Adventure.QueryTaiwuInAny())
            {
                return false;
            }

            if (DomainManager.World.GetAdvancingMonthState() != 0 || DomainManager.TaiwuEvent.IsShowingEvent)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string OnStay()
    {
        var data = new SerializableModData();
        data.Set("charId", GetCurrentAyaId());
        TryCallNoRet(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "Register", data);
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultCodeKey, "stay");
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultMessageKey, "阿雅认真点头，暂且留在此处，等你需要时再请她相助。");
        return Taiwu_EventPackage_GoodPartnerAya.ResultGuid;
    }

    private string OnLater()
    {
        var data = new SerializableModData();
        data.Set("charId", GetCurrentAyaId());
        TryCallNoRet(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "Register", data);
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultCodeKey, "later");
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultMessageKey, "阿雅暂且留在此处。");
        return Taiwu_EventPackage_GoodPartnerAya.ResultGuid;
    }

    private int GetCurrentAyaId()
    {
        var charId = -1;
        ArgBox?.Get(Taiwu_EventPackage_GoodPartnerAya.RoleKey, ref charId);
        return charId;
    }
}

internal sealed class AyaInteractEvent : AyaEventBase
{
    public AyaInteractEvent()
        : base(Taiwu_EventPackage_GoodPartnerAya.InteractGuid, 4, isHeadEvent: true)
    {
        EventSortingOrder = 80;
        EventOptions = new[]
        {
            new TaiwuEventOption
            {
                OptionKey = "purify",
                OptionGuid = "c1dab2e8-275f-441b-a285-1463f67bbbe9",
                OnOptionSelect = OnPurify
            },
            new TaiwuEventOption
            {
                OptionKey = "join",
                OptionGuid = "24967f3c-d6e5-47ac-87e2-0c53cd27a5c2",
                OnOptionSelect = OnJoin,
                OnOptionVisibleCheck = () => !GetResultBool(TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "GetStatus", out var status) ? status : null, "joined", false)
            },
            new TaiwuEventOption
            {
                OptionKey = "leave",
                OptionGuid = "f537ae8c-8f7d-4dad-912b-b70d0aec0315",
                OnOptionSelect = OnLeave,
                OnOptionVisibleCheck = () => GetResultBool(TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "GetStatus", out var status) ? status : null, "joined", false)
            },
            new TaiwuEventOption
            {
                OptionKey = "prepare_uninstall",
                OptionGuid = "ab0c12cf-3b77-4665-95fd-196e50d0aa02",
                OnOptionSelect = OnPrepareUninstall
            },
            new TaiwuEventOption
            {
                OptionKey = "bye",
                OptionGuid = "4d5346da-12a6-4c27-860e-31d5a5268879",
                OnOptionSelect = () => string.Empty
            }
        };
    }

    public override bool OnCheckEventCondition()
    {
        var clickedCharId = -1;
        var clickedTemplateId = -1;
        if (ArgBox == null)
        {
            return false;
        }

        ArgBox.Get(EventTriggerParameter.DefValue.CharacterId.ArgBoxKey, ref clickedCharId);
        ArgBox.Get(EventTriggerParameter.DefValue.CharacterTemplateId.ArgBoxKey, ref clickedTemplateId);

        if (!TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "GetStatus", out var status))
        {
            return false;
        }

        if (GetResultBool(status, "uninstallPrepared", false))
        {
            return false;
        }

        var ayaCharId = GetResultInt(status, "ayaCharId", -1);
        var templateId = GetResultInt(status, "fixedCharacterTemplateId", -1);
        if (clickedCharId <= 0 || ayaCharId <= 0 || clickedCharId != ayaCharId)
        {
            return false;
        }

        if (templateId >= 0 && clickedTemplateId >= 0 && clickedTemplateId != templateId)
        {
            return false;
        }

        ArgBox.Set(Taiwu_EventPackage_GoodPartnerAya.RoleKey, ayaCharId);
        return true;
    }

    private string OnPurify()
    {
        TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "PurifyCurrentBlock", new SerializableModData(), out var result);
        SetResult(result);
        return Taiwu_EventPackage_GoodPartnerAya.ResultGuid;
    }

    private string OnJoin()
    {
        var data = new SerializableModData();
        data.Set("charId", GetStatusAyaId());
        TryCallNoRet(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "Join", data);
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultCodeKey, "join");
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultMessageKey, "阿雅认真点头，背好小小行囊。往后太吾行至何处，她便随行至何处。");
        return Taiwu_EventPackage_GoodPartnerAya.ResultGuid;
    }

    private string OnLeave()
    {
        var data = new SerializableModData();
        data.Set("charId", GetStatusAyaId());
        TryCallNoRet(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "Leave", data);
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultCodeKey, "leave");
        ArgBox?.Set(Taiwu_EventPackage_GoodPartnerAya.ResultMessageKey, "阿雅暂且留在此处。若你再来寻她，她仍会帮你。");
        return Taiwu_EventPackage_GoodPartnerAya.ResultGuid;
    }

    private string OnPrepareUninstall()
    {
        var data = new SerializableModData();
        data.Set("charId", GetStatusAyaId());
        TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "PrepareUninstall", data, out var result);
        SetResult(result);
        return Taiwu_EventPackage_GoodPartnerAya.ResultGuid;
    }

    private int GetStatusAyaId()
    {
        return TryCall(Taiwu_EventPackage_GoodPartnerAya.MethodPrefix + "GetStatus", out var status)
            ? GetResultInt(status, "ayaCharId", -1)
            : -1;
    }
}

internal sealed class AyaResultEvent : AyaEventBase
{
    public AyaResultEvent()
        : base(Taiwu_EventPackage_GoodPartnerAya.ResultGuid, -1, isHeadEvent: false)
    {
        EventOptions = new[]
        {
            new TaiwuEventOption
            {
                OptionKey = "ok",
                OptionGuid = "a6e58a8e-9ead-40bd-a1f0-1482eda9994e",
                OnOptionSelect = () => string.Empty
            }
        };
    }

    public override bool OnCheckEventCondition()
    {
        if (ArgBox == null)
        {
            return false;
        }

        if (!ArgBox.Contains<int>(Taiwu_EventPackage_GoodPartnerAya.RoleKey))
        {
            BindAyaFromStatus();
        }

        return true;
    }

    public override string GetReplacedContentString()
    {
        var message = string.Empty;
        ArgBox?.Get(Taiwu_EventPackage_GoodPartnerAya.ResultMessageKey, ref message);
        return string.IsNullOrEmpty(message) ? EventContent : message;
    }
}

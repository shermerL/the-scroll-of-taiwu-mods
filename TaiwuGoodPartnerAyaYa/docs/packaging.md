# 太吾好伙伴阿雅打包说明

## 可打包目录

实际给游戏或创意工坊使用的目录是：

```text
TaiwuGoodPartnerAyaYa/ModPackage/TaiwuGoodPartnerAya/
```

当前包含：

```text
Config.Lua
Plugins/TaiwuGoodPartnerAya.dll
Config/AyaConfig.json
Events/EventLib/Taiwu_EventPackage_GoodPartnerAya.dll
Events/EventLanguages/Taiwu_EventPackage_GoodPartnerAya_Language_CN.txt
Assets/Images/ayaya-event-portrait-taiwu-style.png
workshop/WorkshopCover.jpg
```

其中 `Plugins/TaiwuGoodPartnerAya.dll` 是后端插件，`Events/EventLib/Taiwu_EventPackage_GoodPartnerAya.dll` 是官方事件包，`Config/AyaConfig.json` 是安全配置，`workshop/WorkshopCover.jpg` 是工坊封面。

## 编译后端插件

```bash
cd /Users/shermer/Documents/Mod/the-scroll-of-taiwu-mods/TaiwuGoodPartnerAyaYa/src
dotnet build TaiwuGoodPartnerAya.csproj
```

编译成功后，把下面文件复制到可打包目录的 `Plugins/` 下。当前游戏日志确认后端加载器会寻找 `Mod/<ModId>/Plugins/<dll>`：

```text
src/bin/Debug/net8.0/TaiwuGoodPartnerAya.dll
  -> ModPackage/TaiwuGoodPartnerAya/Plugins/TaiwuGoodPartnerAya.dll
```

## 编译事件包

```bash
cd /Users/shermer/Documents/Mod/the-scroll-of-taiwu-mods/TaiwuGoodPartnerAyaYa/event-src
dotnet build Taiwu_EventPackage_GoodPartnerAya.csproj
```

编译成功后，把下面文件复制到可打包目录：

```text
event-src/bin/Debug/net8.0/Taiwu_EventPackage_GoodPartnerAya.dll
  -> ModPackage/TaiwuGoodPartnerAya/Events/EventLib/Taiwu_EventPackage_GoodPartnerAya.dll
```

语言文件必须放在：

```text
ModPackage/TaiwuGoodPartnerAya/Events/EventLanguages/Taiwu_EventPackage_GoodPartnerAya_Language_CN.txt
```

`Config.Lua` 中必须声明：

```lua
EventPackages = {"Taiwu_EventPackage_GoodPartnerAya.dll"}
```

## 固定人物模板配置

阿雅的固定人物模板不需要玩家填写。Mod 包内的 `Config/Character_Aya.lua` 会通过官方 Mod 配置补丁机制新增一条 `Character` 配置：

```text
SrcConfigRefName = "京畿女"
DestConfigRefName = "TaiwuGoodPartnerAya.Aya"
TemplateId = 12001
```

代码内部固定使用 `12001` 创建/识别阿雅。这个 id 一旦发布不要变更，否则旧存档里已经创建的阿雅会与新版模板脱节。

## 首版后端接口

后端插件注册了这些官方 ModMethod：

| 方法 | 用途 |
| --- | --- |
| `TaiwuGoodPartnerAya.Ensure` | 按配置固定人物模板创建或复用阿雅，并可移动到太吾当前格。 |
| `TaiwuGoodPartnerAya.TriggerIntro` | 后端主动排队阿雅初遇事件，主要用于调试或后续触发入口。 |
| `TaiwuGoodPartnerAya.Register` | EventPackage 创建固定人物后，把角色 id 注册为阿雅。参数：`charId`。 |
| `TaiwuGoodPartnerAya.Join` | 开启阿雅随行：记录本 Mod 的随行状态，并把阿雅移动到太吾当前格。可选参数：`charId`。 |
| `TaiwuGoodPartnerAya.Leave` | 关闭阿雅随行：只修改本 Mod 的随行状态，不调用原版同道离队。可选参数：`charId`。 |
| `TaiwuGoodPartnerAya.PrepareUninstall` | 卸载前准备：离队、停止自动初遇和净化、写入 `UninstallPrepared`。可选参数：`charId`。 |
| `TaiwuGoodPartnerAya.PurifyCharacter` | 净化指定 NPC。参数：`targetCharId`。 |
| `TaiwuGoodPartnerAya.PurifyCurrentBlock` | 扫描太吾当前格子的普通、感染、固定人物集合，但只净化当前格内可治疗、非固定剧情、未死亡且确有玄灰/相枢感染的目标。 |
| `TaiwuGoodPartnerAya.GetStatus` | 返回阿雅角色 id、是否入队、冷却日期、卸载准备状态、当前格可净化目标数等状态。 |

返回值统一使用 `SerializableModData`，至少包含 `success`、`code`、`message`、`purifiedCount`、`cooldownUntilDate`。

## 已接上的事件链路

- 太吾村之后、自由行动换格时，头事件检查阿雅初遇。
- 初遇事件调用 `TaiwuGoodPartnerAya.Ensure` 创建或复用固定人物，并把阿雅移动到太吾当前格。
- 初遇同意或专属交互选择随行后调用 `TaiwuGoodPartnerAya.Join`，阿雅不进入 GearMate，同步位置由 `Events.OnTaiwuMove`、读档和月结后处理。
- 初遇暂缓后调用 `TaiwuGoodPartnerAya.Register`，阿雅留在当前格，后续可点击交互。
- 点击阿雅固定人物会进入专属事件。
- 专属事件可调用 `TaiwuGoodPartnerAya.PurifyCurrentBlock` 净化当前格可治疗 NPC。
- 专属事件可调用 `TaiwuGoodPartnerAya.PrepareUninstall` 做卸载前准备，成功后本 Mod 不再自动触发初遇或净化。
- 结果事件会展示后端返回的成功、冷却、无目标或错误信息。

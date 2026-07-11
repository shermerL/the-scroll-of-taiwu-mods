# 太吾好伙伴阿雅

《太吾绘卷》官方事件包方向的角色 Mod。目标是添加一位固定特殊人物“阿雅”，并通过原版事件对白与人物交互承载玄灰净化功能。

## 当前定位

- 使用官方 `EventPackages` 承载对白、选项和人物交互，不自绘剧情 UI。
- 后端插件负责记录阿雅角色、存档状态、卸载准备和能力效果。
- 每个特殊角色保持为独立 Mod，便于后续扩展“太吾的好伙伴”系列。

## 首版核心能力

- 阿雅会以固定人物身份出现在太吾当前地块，玩家点击阿雅后进入专属交互。
- 阿雅可进入“随行”状态：太吾移动、读档或过月后，后端会把阿雅同步到太吾当前地块。
- 随行不使用原版 `GearMate` 同道系统，不占同道位，不进入同道战斗选择。
- 阿雅拥有“天生净化力量”，可治疗当前格子内可交互 NPC 的玄灰相关状态。
- 净化成功后有 6 个月冷却，传剑后冷却不重置。
- 能力表现走人物点击后的官方交互选项，而不是额外窗口。

## 当前产物

- 源码：`src/TaiwuGoodPartnerAya.csproj`
- 可打包目录：`ModPackage/TaiwuGoodPartnerAya`
- 后端插件：`Plugins/TaiwuGoodPartnerAya.dll`
- 事件包：`Events/EventLib/Taiwu_EventPackage_GoodPartnerAya.dll`
- 语言文件：`Events/EventLanguages/Taiwu_EventPackage_GoodPartnerAya_Language_CN.txt`
- 配置文件：`Config/AyaConfig.json`
- 人物模板补丁：`Config/Character_Aya.lua`

当前已经接入官方 `EventPackages` 的固定人物点击交互。后端插件提供 `TaiwuGoodPartnerAya.Ensure`、`TaiwuGoodPartnerAya.TriggerIntro`、`TaiwuGoodPartnerAya.Register`、`TaiwuGoodPartnerAya.Join`、`TaiwuGoodPartnerAya.Leave`、`TaiwuGoodPartnerAya.PrepareUninstall`、`TaiwuGoodPartnerAya.PurifyCharacter`、`TaiwuGoodPartnerAya.PurifyCurrentBlock`、`TaiwuGoodPartnerAya.GetStatus` 等接口。其中 `Join`/`Leave` 只切换本 Mod 的随行状态，不再调用原版 GearMate 入队/离队。

阿雅的固定人物模板由 `Config/Character_Aya.lua` 随 Mod 内置，玩家不需要填写模板 id。

## 文档

- [设计记录](docs/design.md)
- [对白草案](docs/dialogue.md)
- [资产记录](docs/assets.md)
- [兼容性与更新策略](docs/compatibility.md)
- [打包说明](docs/packaging.md)
- [固定人物与地图头像接口分析](docs/2026-07-12-太吾绘卷固定人物与地图头像接口分析.md)
- [测试问题复盘](docs/2026-07-12-太吾好伙伴阿雅测试问题复盘.md)

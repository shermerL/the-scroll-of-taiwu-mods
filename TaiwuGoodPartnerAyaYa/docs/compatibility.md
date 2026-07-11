# 太吾好伙伴阿雅兼容性与更新策略

## 结论

这个 Mod 可以做成长期更新且尽量不伤存档，但需要从第一版开始遵守稳定性规则：

- 不随意修改 Mod 的识别信息。
- 不删除或改义已经写入存档的 Mod 数据。
- 不复用别的 Mod 可能用到的事件 GUID、配置 id、资源名。
- 后端接口保持向后兼容，新增接口优先，不随便改旧接口签名。
- 所有操作做成幂等：重复注册、重复入队、重复净化、重复触发初遇，都不能让存档进入异常状态。

## 存档更新风险

### 1. ModId 变化

后端数据通过 `DomainManager.Mod.SetInt/SetBool/...` 写入存档，并由当前 Mod 的 `ModIdStr` 隔离。只要 `ModIdStr` 变了，旧数据就读不到，表现为：

- 阿雅角色 id 丢失。
- 冷却时间丢失。
- 已触发初遇状态丢失。
- 可能重复触发初遇或重复创建角色。

稳定策略：

- `Config.Lua` 的标题可以显示为中文，但内部包目录和 DLL 名保持 `TaiwuGoodPartnerAya`。
- 创意工坊正式发布后，不要换成另一个全新条目。
- 不要把这个 Mod 拆成另一个独立 Mod 后要求玩家覆盖旧版。
- 如果必须改 ModId，需要写一次专门迁移：从旧 ModId 读取旧 key，再写入新 ModId。

### 2. 存档 Key 变化

当前后端使用的存档 key：

| Key | 用途 |
| --- | --- |
| `TaiwuGoodPartnerAya.CharacterId` | 已创建的阿雅角色 id。 |
| `TaiwuGoodPartnerAya.Joined` | 阿雅是否处于跟随状态。 |
| `TaiwuGoodPartnerAya.IntroTriggered` | 初遇是否已触发。 |
| `TaiwuGoodPartnerAya.PurifyCooldownUntilDate` | 净化冷却结束日期。 |

稳定策略：

- 这些 key 一旦发布，不要删除，不要改名，不要换含义。
- 新功能用新 key，例如 `TaiwuGoodPartnerAya.V2.SomeFeatureEnabled`。
- 如果旧 key 写错了，也保留读取兼容，再迁移到新 key。
- 不要把重要状态只放在配置文件里，必须放入存档级 Mod 数据。

### 3. 角色模板或事件配置变化

阿雅应作为固定模板人物创建。如果未来修改人物模板 id 或事件 GUID，旧存档可能出现：

- 旧阿雅还在，新版本又创建一个新阿雅。
- 点击旧阿雅没有新交互。
- 事件包找不到旧事件或跳到错误事件。

稳定策略：

- 固定人物模板 id 发布后尽量不改。
- 事件 GUID 发布后永久保留。
- 要替换事件时，新增 GUID，并保留旧 GUID 的转跳或兼容处理。
- `TaiwuGoodPartnerAya.Register` 只注册一个有效阿雅；如果发现旧阿雅存在，不再创建第二个。

## 后端接口兼容

当前已发布候选接口：

| 方法 | 兼容要求 |
| --- | --- |
| `TaiwuGoodPartnerAya.Register` | 参数 `charId` 长期保留；重复调用应安全。 |
| `TaiwuGoodPartnerAya.Join` | 可选 `charId` 长期保留；已入队时重复调用不应异常。 |
| `TaiwuGoodPartnerAya.Leave` | 可选 `charId` 长期保留；已离队时重复调用不应异常。 |
| `TaiwuGoodPartnerAya.PrepareUninstall` | 可选 `charId` 长期保留；执行后写入 `UninstallPrepared`，后续自动逻辑应停用。 |
| `TaiwuGoodPartnerAya.PurifyCharacter` | 参数 `targetCharId` 长期保留；无目标或不可治疗时只返回失败，不抛异常。 |
| `TaiwuGoodPartnerAya.PurifyCurrentBlock` | 无参数调用长期保留；无可治疗目标时不消耗冷却。 |
| `TaiwuGoodPartnerAya.GetStatus` | 返回字段只增不删。 |

新增功能时优先新增方法，例如：

- `TaiwuGoodPartnerAya.GetInteractionState`
- `TaiwuGoodPartnerAya.TryTriggerIntro`
- `TaiwuGoodPartnerAya.PurifyCharactersV2`

不要直接把旧方法参数换成另一套结构。

## 与其他特殊角色 Mod 的冲突

### 低风险部分

后端 ModMethod 是按 `ModIdStr` 隔离注册的；本 Mod 仍统一使用 `TaiwuGoodPartnerAya.*` 方法名，避免后续复制模板时出现语义混乱。

存档数据同样按 `ModIdStr` 隔离；本 Mod 仍统一使用 `TaiwuGoodPartnerAya.*` key，方便以后做跨版本迁移和问题排查。

特殊同道也比普通同道安全。阿雅走官方 `JoinGroup` 固定模板分支，会加入特殊同道集合，不占普通三人同道名额。

### 高风险部分

真正容易冲突的是全局资源与全局配置：

- 固定人物模板 id。
- 事件 GUID。
- EventPackage 内部类名、脚本名、语言 key。
- 图片、AssetBundle、资源路径。
- 地图自定义按钮 id 或角色菜单控制配置 id。
- 如果未来做 Harmony patch，patch 同一个原版方法也可能冲突。

规避策略：

- 所有 GUID 用本 Mod 专属前缀，例如 `shermer.goodpartner.aya.*`。
- 所有资源路径用专属前缀，例如 `GoodPartnerAya/Images/...`。
- 所有语言 key 用专属前缀，例如 `GoodPartnerAya_Intro_001`。
- 不占用别人可能手写的通用名字，例如 `intro_event`、`heal_event`、`character_001`。
- 尽量不做 Harmony patch；优先用官方 EventPackages 和 ModMethod。
- 如果必须 patch，patch 要短、条件要严格、异常要吞掉并记录 warning/error。

## 用户删除 Mod 是否会坏档

结论：如果这个 Mod 只是加载后端 DLL、写入 Mod 数据，删除后通常不会坏档；但只要正式版创建了“阿雅”这个固定人物，并让存档里保存了对 Mod 自定义模板、事件、资源的引用，用户直接删除 Mod 就有坏档或半坏档风险。

风险分层：

| 内容 | 删除 Mod 后风险 | 原因 |
| --- | --- | --- |
| 只写 `DomainManager.Mod` 存档数据 | 低 | 数据按 ModId 保存；Mod 不在时通常只是没人读取。 |
| 后端 DLL 提供 ModMethod | 低到中 | 只要没有事件还在调用这些方法，删除后不会主动执行。 |
| 自定义事件仍在队列或正在显示 | 中 | 存档可能保存当前事件状态，删除事件包后可能找不到事件。 |
| 自定义固定人物模板 | 高 | 存档角色可能引用已不存在的模板 id、头像、菜单配置或资源。 |
| 自定义图片/AssetBundle/Spine 资源 | 中到高 | 角色显示时可能找不到资源，轻则空图，重则报错。 |
| Harmony patch 修改原版流程 | 中到高 | 如果 patch 曾写入非原版状态，删除后原版流程可能不认识。 |

因此正式发布时不要承诺“随便删除绝对安全”。更稳妥的文案是：

> 卸载前请在游戏内使用“阿雅告别/卸载准备”选项，保存并重启游戏后再移除 Mod 文件。

## 安全卸载设计

当前版本已经提供游戏内卸载准备流程，而不是让用户直接删文件：

1. 点击阿雅，选择“我欲暂别此缘，先作妥善安排。”
2. 后端确认当前不在战斗、奇遇、月结流程中。
3. 如果阿雅在特殊同道中，调用官方 `GearMateLeaveGroup` 离队逻辑。
4. 写入 `TaiwuGoodPartnerAya.Joined = false`。
5. 写入 `TaiwuGoodPartnerAya.IntroTriggered = true`，避免重新安装后再次自动触发初遇。
6. 写入 `TaiwuGoodPartnerAya.UninstallPrepared = true`，后续自动创建、入队、净化、交互事件都会停止。
7. 提示玩家保存、退出游戏，再删除 Mod。

需要谨慎的点：

- 不要强行删除角色对象，除非确认原版有安全删除固定人物的接口。
- 不要清理别的 Mod 或原版写入的人物关系、物品、经历。
- 当前首版不额外生成并发放自定义物品；后续如果让阿雅携带物品，优先让玩家取回，或通过事件明确说明会随阿雅离开。
- 如果自定义模板无法在卸载后继续解析，最安全策略是要求玩家在卸载前让阿雅离开并完成卸载准备。

## 降低坏档概率的实现策略

首版和后续版本应尽量做到：

- 阿雅的核心运行状态都由本 Mod 读取，不让原版在无 Mod 时频繁访问自定义逻辑。
- 尽量少给阿雅绑定自定义不可替代资源；PNG 缺失应有默认图兜底。
- 初遇事件和交互事件不要长期停留在事件队列里；执行后立刻落盘状态。
- `TaiwuGoodPartnerAya.GetStatus`、初遇事件、交互事件和后端接口都会检查 `TaiwuGoodPartnerAya.UninstallPrepared = true`，不再触发初遇、不再自动同步位置、不再执行净化相关逻辑。
- 每个版本保留旧事件 GUID 的兼容跳转，避免用户更新时卡在旧事件中。
- 发布说明明确提醒：不要在事件对话中、阿雅正在入队/离队时、或净化刚执行到一半时删除 Mod。

## 多特殊角色共存策略

如果以后每个特殊角色一个 Mod，建议每个角色都遵守同一个结构：

```text
TaiwuGoodPartnerAya/
TaiwuGoodPartnerOther/
```

每个角色独立：

- 独立 DLL。
- 独立 Config.Lua。
- 独立 ModId。
- 独立存档 key 前缀。
- 独立事件 GUID 前缀。
- 独立资源路径前缀。

共享内容只放文档或公共设计规范，不要让多个角色共用一个必须存在的全局状态。否则玩家只装其中一个角色时容易缺依赖。

## 更新前检查清单

每次发新版前检查：

- `Config.Lua` 是否仍是同一个 Mod 条目。
- `BackendPlugins` 是否仍包含 `TaiwuGoodPartnerAya.dll`。
- 旧存档 key 是否仍能读取。
- `TaiwuGoodPartnerAya.GetStatus` 是否能在没有阿雅、旧阿雅、已入队、已离队、冷却中这些状态下返回正常结果。
- `TaiwuGoodPartnerAya.PurifyCurrentBlock` 只处理太吾当前地块、允许治疗、非太吾、非阿雅、非固定剧情人物、未死亡且存在玄灰或相枢感染的角色；无目标时不消耗冷却。
- 重复调用 `TaiwuGoodPartnerAya.Register` 不会创建第二个阿雅。
- `TaiwuGoodPartnerAya.PrepareUninstall` 成功后，重新加载 Mod 不会再次自动初遇或入队。
- 旧事件 GUID 是否仍存在或有兼容跳转。
- 新增资源路径是否带专属前缀。
- 包目录中只包含运行所需文件，不包含 `src/bin`、`src/obj`。

## 后续仍可补强的点

当前版本已经接入固定人物创建、初遇门禁、当前地块净化和卸载准备。后续发布前仍建议补：

- 在 `TaiwuGoodPartnerAya.GetStatus` 返回 `archiveSchemaVersion`。
- 写一个 `TaiwuGoodPartnerAya.MigrateArchiveData` 或在 `OnLoadedArchiveData` 中自动迁移旧 key。
- `TaiwuGoodPartnerAya.Join` 增加“已经跟随则直接成功”的返回式接口，方便事件包判断。
- 固定人物模板 id、事件 GUID、语言 key、资源路径确定后写入本文档，作为以后不随意改动的兼容合同。

# 太吾好伙伴阿雅 资产记录

## 当前资产

| 路径 | 用途 | 备注 |
| --- | --- | --- |
| `workshop/WorkshopCover.jpg` | Steam 创意工坊封面 | 当前使用原截图搞怪版，约 184KB，已控制在 1MB 内。 |
| `Assets/Images/ayaya-event-portrait-taiwu-style.png` | 太吾风格事件半身立绘 | 已由全身候选图扣透明底并裁成半身图，供事件/人物交互立绘接入。 |
| `Assets/Images/ayaya-fullbody-transparent.png` | 太吾风格全身透明图 | 保留为后续封面、详情或前端裁切备用。 |
| `CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt` | 游戏内 AvatarData | 原生捏脸数据，只影响拼装头像，不等同于 PNG 立绘资源。 |
| `ModResources/Textures/NpcFace/BigFace/NpcFace_taiwu_good_partner_aya.png` | 固定人物大立绘 | 对应 `FixedAvatarName` 的 BigFace 资源，用于事件窗口和大头像。 |
| `ModResources/Textures/NpcFace/NormalFace/NpcFace_taiwu_good_partner_aya.png` | 固定人物普通头像 | 对应人物详情等普通头像尺寸。 |
| `ModResources/Textures/NpcFace/SmallFace/NpcFace_taiwu_good_partner_aya.png` | 固定人物小头像 | 对应地图左侧角色列表、日志等小头像尺寸。 |
| `TempUnusedImages/` | 草稿和候选图片 | 只作备份，不进入正式 ModPackage。 |

## Steam 封面限制

- Steam 创意工坊封面需要保持在 1MB 以内。
- 当前正式上传文件使用 `workshop/WorkshopCover.jpg`。
- 源图不受该限制；当前未使用源图已移动到 `TempUnusedImages/`，后续可重新挑选或裁切。

## 首版 PNG 方案

首版先使用 PNG 静态图，不做 Spine 动态立绘。

原因：

- 阿雅的核心风险在玩法接入：固定人物创建、特殊同道、事件交互、玄灰净化、传剑状态处理。
- Spine 动态立绘需要额外的编辑器、拆层、骨骼动画、Unity AssetBundle 打包和游戏资源索引测试。
- 先用 PNG 可以更快进入游戏验证，避免在美术资产链路上卡住。

首版推荐使用：

- 事件/人物交互立绘：`Assets/Images/ayaya-event-portrait-taiwu-style.png`
- 创意工坊封面：`workshop/WorkshopCover.jpg`
- 游戏内固定人物 PNG：`ModResources/Textures/NpcFace/*/NpcFace_taiwu_good_partner_aya.png`
- 游戏内 AvatarData 兜底：`CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt`

## 2026-07-12 外观接入说明

- 当前版本不再只依赖 `京畿女` 随机外观。角色模板已设置 `FixedAvatarName = "NpcFace_taiwu_good_partner_aya"`，同时保留 `AvatarDataPath = "TaiwuGoodPartnerAya/Aya.txt"` 作为兜底。
- 后端插件启动时会把包内 `CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt` 复制到游戏可读取的 `StreamingAssets/CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt`，再创建或迁移阿雅外观。
- 已经在旧版本中生成过的阿雅，会在下次 `EnsureAya`、交互注册或随行同步时尝试应用固定 AvatarData。
- 前端固定人物 PNG 加载路径已确认：`CharacterAvatar` 会用 `FixedAvatarName` 拼出 `NpcFace/<BigFace|NormalFace|SmallFace>/<FixedAvatarName>`，再调用 `ResLoader.LoadModOrGameResource`。
- `LoadModOrGameResource` 会优先从已加载 Mod 的 `ModResources/Textures` 中查询同名 path key；找不到才回退游戏内置 `RemakeResources/Textures`。
- 因此 Mod 内应放置 `ModResources/Textures/NpcFace/BigFace/NpcFace_taiwu_good_partner_aya.png` 等三档文件，而不是只放在 `Assets/Images`。
- `0.1.13` 起额外加入轻量前端插件 `TaiwuGoodPartnerAyaFrontend.dll`，启动时会把三档头像 key 主动注册进官方 `TextureCenter` 的 `ModTexture_<ModId>` 纹理组。它不做 UI 覆盖绘制，只补齐资源索引，避免前端回退到本体 `RemakeResources/Textures` 后报 `Failed to load resource`。
- `0.1.14` 继续保留该资源注册插件，同时修正阿雅随行/回村交互的结果页逻辑。

## 动态立绘判断与后续

从当前游戏文件看，太吾动态立绘不是普通 PNG 序列帧路线：

- 游戏带有 `spine-csharp.dll`、`spine-unity.dll`。
- 本体资源中存在大量 `StreamingAssets/PreSpine/pre_spine_*.uab`。
- 常见命名包括 `pre_spine_npcface_*`、`pre_spine_eventcharacterback_*`、`pre_spine_avatar_hair_*`、`pre_spine_avatar_clothing_*`。

因此正式动态立绘应按 Spine + Unity AssetBundle 处理，而不是直接把 GIF 或 PNG 序列塞进事件包。该项作为后续增强，不进入首版必做范围。

## 推荐实现路线

整体可分两步走：

1. 先使用静态头像/立绘接入角色，确保阿雅能创建、入队、显示、交互、净化玄灰。
2. 再制作 Spine 动态立绘包，并通过官方资源加载路径或 Mod 资源加载路径替换阿雅的固定人物头像/事件立绘。

正式 Spine 资产建议包含：

- 角色半身拆件图层：脸、头发、发饰、身体、衣袖、飘带、手、药囊、灵光。
- `idle` 动画：轻微呼吸、头发和飘带慢摆、眼神微动。
- `purify` 动画：抬手、净化光扩散、玄灰散去。
- `tired` 动画：净化冷却时使用，表现“累坏了，需要缓一缓”。
- 小尺寸头像或圆形图标版本，用于地图固定人物入口。

## 后续动态立绘接入前需要确认

- 游戏是否允许 Mod 自带 `.uab` 被官方资源管理器加载。
- 固定人物模板中引用 Spine 资源的字段名和资源 id。
- 自定义 `pre_spine_*` 包是否需要 `packagecontroller.uab` 或资源索引登记。
- 创意工坊打包是否能包含自定义 AssetBundle。

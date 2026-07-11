# 太吾好伙伴阿雅 资产记录

## 当前资产

| 路径 | 用途 | 备注 |
| --- | --- | --- |
| `workshop/WorkshopCover.jpg` | Steam 创意工坊封面 | 当前使用原截图搞怪版，约 184KB，已控制在 1MB 内。 |
| `Assets/Images/ayaya-event-portrait-taiwu-style.png` | 太吾风格事件半身立绘 | 已由全身候选图扣透明底并裁成半身图，供事件/人物交互立绘接入。 |
| `Assets/Images/ayaya-fullbody-transparent.png` | 太吾风格全身透明图 | 保留为后续封面、详情或前端裁切备用。 |
| `CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt` | 游戏内人物头像数据 | 原生 AvatarData，用来固定人物页、左侧列表、地图头像的游戏内捏脸外观，避免继续随机生成普通 NPC 脸。 |
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
- 游戏内头像/人物页外观：`CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt`
- 头像/侧边栏临时图：若前端允许直接读 PNG，可从 `ayaya-event-portrait-taiwu-style.png` 裁切；否则先使用 AvatarData 固定的原生头像。

## 2026-07-12 外观接入说明

- 当前版本不再只依赖 `京畿女` 随机外观。角色模板已设置 `AvatarDataPath = "TaiwuGoodPartnerAya/Aya.txt"`。
- 后端插件启动时会把包内 `CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt` 复制到游戏可读取的 `StreamingAssets/CharacterAvatarData/TaiwuGoodPartnerAya/Aya.txt`，再创建或迁移阿雅外观。
- 已经在旧版本中生成过的阿雅，会在下次 `EnsureAya`、交互注册或随行同步时尝试应用固定 AvatarData。
- PNG 半身图已经扣透明底并放入包内，但是否能直接替换事件右侧大立绘，还取决于前端事件 UI 是否接受 Mod 包内 PNG 路径。若游戏仍显示 AvatarData 头像，下一步需要接前端资源/事件立绘加载层。

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

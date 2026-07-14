# 太吾加加加

《太吾绘卷》前端 Mod，用于在游戏内按分类、品级和名称筛选常规物品，并添加到当前太吾角色身上。

## 功能

- 默认 `Home` 呼出/隐藏界面
- 支持在界面内修改热键，并持久化写入游戏用户数据目录下的 `TaiwuJiaJiaJia/TaiwuJiaJiaJia.settings.ini`
- 按分类、品级、名称筛选物品
- 物品网格显示图标和品级颜色
- 右侧详情面板显示物品信息
- 默认过滤任务、剧情、特殊物品
- 任务/特殊物品可查看详情，但不可添加
- 添加数量支持手动输入，并限制在 `1 - 9999`
- 仅记录 warning/error 日志

## 目录

- `src/`：Mod 源码与 `Config.Lua`
- `ModPackage/`：最终要复制到游戏 `Mod` 目录的发布结构，只看这个目录就知道实际 Mod 需要什么
- `workshop/`：Steam 创意工坊封面和介绍文案

## 构建

项目当前引用本机游戏安装目录中的程序集。构建前需要确认 `src/TaiwuItemAdderFrontend.csproj` 里的 `HintPath` 指向你的《太吾绘卷》安装目录。

这些游戏本体文件只用于编译引用，不要打进 Mod 包：

- `Assembly-CSharp.dll`
- `GameData.Shared.dll`
- `TaiwuModdingLib.dll`
- `UnityEngine*.dll`

```bash
dotnet build TaiwuJiaJiaJia/src/TaiwuItemAdderFrontend.csproj -c Release
```

构建后将生成的 `TaiwuItemAdderFrontend.dll` 放入 Mod 目录：

```text
TaiwuJiaJiaJia/ModPackage/TaiwuItemAdderFrontend/
  Config.Lua
  Plugins/
    TaiwuItemAdderFrontend.dll
```

然后将整个 `TaiwuItemAdderFrontend/` 目录复制到游戏的 `Mod` 目录下。

## 实际安装

最终游戏目录应当长这样：

```text
The Scroll Of Taiwu/
  Mod/
    TaiwuItemAdderFrontend/
      Config.Lua
      Plugins/
        TaiwuItemAdderFrontend.dll
```

除上述两个文件外，其它源码、README、工坊素材都不是游戏运行必需文件。

## 注意

本 Mod 只建议用于补遗、测试或救急。过度添加物品可能明显降低游戏体验。

游戏更新后，如果接口变动，Mod 可能需要重新适配。

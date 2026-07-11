# 太吾加加加 ModPackage

这里放的是最终要复制到《太吾绘卷》`Mod` 目录下的文件结构。

## 实际需要放进游戏的东西

游戏只需要这个目录：

```text
TaiwuItemAdderFrontend/
  Config.Lua
  Plugins/
    TaiwuItemAdderFrontend.dll
```

把整个 `TaiwuItemAdderFrontend/` 文件夹复制到游戏的 `Mod` 目录下即可。

```text
The Scroll Of Taiwu/
  Mod/
    TaiwuItemAdderFrontend/
      Config.Lua
      Plugins/
        TaiwuItemAdderFrontend.dll
```

## 这些东西不需要放进游戏 Mod 目录

- `src/`：源码，只用于开发和重新编译
- `workshop/`：Steam 创意工坊封面和介绍文案
- `README.md`：说明文档
- `.gitignore`、`.git/`：Git 仓库文件
- 游戏本体的 `Assembly-CSharp.dll`、`GameData.Shared.dll`、`TaiwuModdingLib.dll` 等：只用于编译引用，不随 Mod 打包

## DLL 怎么来

`TaiwuItemAdderFrontend.dll` 由源码编译得到。它因为是构建产物，不提交进 GitHub；但本地打包时需要放在：

```text
TaiwuItemAdderFrontend/Plugins/TaiwuItemAdderFrontend.dll
```

`Plugins/.gitkeep` 只是为了让 GitHub 上能看到 `Plugins/` 目录。实际发布时不需要关心它。

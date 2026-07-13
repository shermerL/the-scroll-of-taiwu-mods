# 太吾好伙伴Manager

太吾好伙伴系列的独立管理器。

首版功能：

- `F10` 呼出/隐藏管理窗口。
- 自动扫描当前已加载的太吾好伙伴 Mod。
- 显示伙伴状态。
- 调用伙伴 Mod 暴露的 `PrepareUninstall` 清理接口。
- 显示每一步执行过程，方便测试与反馈。
- `0.1.2` 起不再依赖 `ModManager.GetLoadedModInfoList()`；会直接读取前端已加载 ModId，并把匹配过程写入窗口日志，便于定位本地/创意工坊 ModId 映射问题。

首版已接入：

- 太吾好伙伴阿雅

## 角色接入协议

Manager 通过 `ModPackage/TaiwuGoodPartnerManager/Config/Partners.txt` 发现角色 Mod。

每行格式：

```text
显示名|Mod标题|后端DLL文件名|方法前缀|说明
```

角色 Mod 需要在后端注册以下方法：

- `{方法前缀}GetStatus`
- `{方法前缀}PrepareUninstall`

`GetStatus` 建议返回：

- `success`
- `message`
- `characterId` 或兼容字段如 `ayaCharId`
- `exists` 或兼容字段如 `ayaExists`
- `joined`
- `placed`
- `dismissed`
- `uninstallPrepared`

`PrepareUninstall` 建议完成角色离队、移出地图、写入不再自动生成/触发的持久状态，并返回 `uninstallPrepared=true`。

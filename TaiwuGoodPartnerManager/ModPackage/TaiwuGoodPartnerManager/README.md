# 太吾好伙伴Manager

`F10` 呼出管理窗口。

用于查看太吾好伙伴系列特殊角色的状态，并执行准备卸载流程。

准备卸载成功后，请保存存档、退出游戏，再关闭或移除对应角色 Mod。

角色列表来自 `Config/Partners.txt`。后续新增角色时，只要该角色 Mod 暴露 `GetStatus` 和 `PrepareUninstall`，并在配置里加一行即可被 Manager 管理。

`0.1.2` 起，Manager 会直接读取前端已加载 ModId，并在处理过程里展示匹配信息，避免本地 Mod 临时 id 映射异常时误判为未加载。

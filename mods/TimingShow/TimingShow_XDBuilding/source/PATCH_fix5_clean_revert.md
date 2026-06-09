# TimingShow 1.3.2 fix5 clean revert

这版从 debug_rewrite2 干净基线重新出包。

## 保留
- 调试面板。
- 判定 Show 当场替换。
- 判定补写次数设置。
- 判定补写默认值改为 0。
- 多人 timing 分玩家记录。

## 没有带入
- 没有 XPerfect 自动兼容/自动跳过顶部标题。
- 没有隐藏 P2 0.0ms。
- 没有多人死亡 timing 特殊屏蔽。

## 下一步建议
用调试面板确认新版真实字段，然后把通用反射兜底收窄成精准路径。
目前截图已确认：
- 方法：scrHitTextMesh.Show
- 文字对象：HitTextMesh(Clone)/TextMeshPro
- 颜色来源：mainText.color

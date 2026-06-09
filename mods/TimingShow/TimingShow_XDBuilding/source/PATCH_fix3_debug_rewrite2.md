# TimingShow 1.3.2 fix3

- 判定文字不再半秒持续重写。现在是：Show 后立即写一次，随后最多额外补写 0~2 次，默认 2。
- 每个判定的替换文本会在 Show 时冻结，所以下一个按键不会把上一个判定文字改成新的 timing。
- 补写只挂 LateUpdate；没有 LateUpdate 时才挂 Update；不再挂 FixedUpdate。
- 新增 UnityModManager 设置页调试面板，默认关闭。可以查看最近一次判定钩子、玩家、判定名、颜色来源、文字对象数量、写入成功/失败次数。
- 为性能减少重复查找：判定文字对象在注册时缓存，补写时不再每次 GetComponentsInChildren。

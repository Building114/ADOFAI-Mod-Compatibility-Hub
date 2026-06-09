# TimingShow 1.3.2 fix6 precise fields

## 改动目标

把判定替换从“猜很多字段”改成“精准新版字段”。

## 精准路径

- 只钩 `scrHitTextMesh.Show`
- 文字对象优先只读 `scrHitTextMesh.mainText`
- 颜色优先只读 `mainText.color`
- 判定名优先从 `Show` 参数读取
- 补写只挂 `scrHitTextMesh.LateUpdate`，没有才用 `Update`
- 补写默认仍是 0

## 保留的小兜底

- 文字对象找不到 `mainText` 时，只试 `text` 和 `textMesh`
- 颜色找不到 `mainText.color` 时，只试 `hitText.color`
- 判定名找不到参数时，只试 `hitMargin` 和 `margin`
- 最后按判定名给默认颜色

## 删除/收窄

- 不再扫描 `scrHitText` / `scrHitTextUI` / `scrJudgementText` 等多个类
- 不再扫描多种 Show 方法名
- 不再用 `GetComponentsInChildren` 扫子物体
- 不再在补写时重新找文字对象

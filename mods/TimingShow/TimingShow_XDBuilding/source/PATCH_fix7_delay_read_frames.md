# TimingShow 1.3.2 fix7 delay read frames

## 新增设置

- `HitTextReadDelayFrames`
- 设置页显示为“延迟读取帧数”
- 范围：0~3
- 默认：0

## 行为

- 0：和 fix6 一样，`scrHitTextMesh.Show` 后立刻读取 `mainText/mainText.color` 并写入。
- 1~3：Show 后先记录这次判定，等待指定帧数，再读取 `mainText/mainText.color` 并写入 timing。

## 注意

- 第一次延迟写入不算“补写”。
- 补写次数仍然单独由 `HitTextExtraRewriteCount` 控制，默认 0。
- timing 数值仍然在 Show 当时冻结，不会被下一个按键改掉。

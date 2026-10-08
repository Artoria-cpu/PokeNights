# 过场动画：四种小脚本

打开 `Assets/Scenes/ShopRaidCutscene.unity`，按 Play。

- **AniCamera**：三个摄像机复用。Start Seconds / End Seconds 分别填 0 / 5.5、5.5 / 11.5、11.5 / 16.5。只开关 Camera 和 Audio Listener，物体保持开启。
- **AniMove**：敌人等待 Wait Seconds，经过 Waypoint 门口，再移动到 Target。Speed 是速度，Animator 一直跑步。两个 Hide 选项控制等待时、到达后隐藏。
- **AniSound**：到 Start Seconds 时同时播放 Sounds 里的声音，到 End Seconds 时停止。可重复挂载。
- **AniScene**：等待 16.5 秒，进入 Tutorial。

Camera 1 上有三个 AniSound：

| 开始 | 结束 | 声音 |
|---|---|---|
| 0.3 | 4.5 | 五路跑步声，进店阶段 |
| 5 | 16.5 | 同样五路跑步声，出店阶段 |
| 11.5 | 16.5 | 玻璃破碎和警铃 |

五路跑步 Audio Source 使用现成跑步录音，开启 Loop，音调各有小幅差异。没有人数统计、随机选择或敌人引用；以后改变敌人出发时间，只需手动改对应 AniSound 的时间。音量在 Audio Source 的 Volume 调整。

直接移动三个摄像机来改画面。旧的 Camera presets 仅作参考。其他场景布置不变。

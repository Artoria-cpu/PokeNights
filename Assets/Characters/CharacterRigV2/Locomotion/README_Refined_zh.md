# 角色动画：真人动捕参考调整版

已更新项目 `E:\unity\saves\My`，场景为 `Assets/Scenes/SampleScene.unity`。
角色、模型材质、原有手臂和膝盖骨骼位置均沿用原来的版本。

## 调整内容

- 走路：采用真人步态的肩胯转动、对侧摆臂和脚掌滚动，调整循环长度与移动速度匹配。
- 跑步：保留真人跑动中的膝盖回收、变化的肘部弯曲和腾空阶段。
- 跳跃：增加约 0.117 秒的屈膝准备，并衔接摆臂起跳、空中姿态和落地缓冲。
- 平滑动捕曲线、处理循环首尾接缝、修正站立和支撑阶段的鞋底高度。手部使用平稳的前臂朝向，减轻原始动捕末端噪声。
- 原有按键与走、跑速度保持不变。动画由 Animator 播放，位移由 CharacterController 控制。

## 操作

打开场景并点击 Play，点击 Game 窗口后操作。

- WASD / 方向键：移动。
- 1：选择走路；2：选择跑步。
- 按住 Shift：临时跑步。
- 空格：跳跃。
- 鼠标右键拖动：转动镜头；滚轮：缩放。

## 动画与文件

| 动画 | 长度 | 循环 |
| --- | ---: | --- |
| Idle | 2.000 秒 | 是 |
| Walk | 0.917 秒 | 是 |
| Run | 0.633 秒 | 是 |
| Prepare | 0.117 秒 | 否 |
| Jump | 0.300 秒 | 否 |
| Fall | 0.300 秒 | 否，播完保持末帧 |
| Land | 0.333 秒 | 否 |

`CharacterLocomotion.blend` 是可继续编辑的 Blender 文件。
`CharacterLocomotion.fbx` 包含角色和上述七段动画。
Unity 内的动画、状态机和控制脚本位于 `Assets/Characters/CharacterRigV2/Locomotion`。

`motion_preview.gif` 是 Blender 渲染的动作预览。跳跃面板加了演示用的抛物线位移；游戏中的实际跳跃由 Unity 控制器处理。
`unity_walk.png`、`unity_run.png`、`unity_jump.png` 是 Unity 播放测试的截图。

## 参考与署名

动作数据来源：[CMU Graphics Lab Motion Capture Database](https://mocap.cs.cmu.edu/)。
下载使用 [Bruce Hahne 的 BVH 转换数据镜像](https://github.com/una-dinosauria/cmu-mocap)，此版本对参考数据进行了骨骼适配、重采样、平滑和接缝修正。

- 走路：[07_01](https://raw.githubusercontent.com/una-dinosauria/cmu-mocap/master/data/007/07_01.bvh)，参考帧 55–186。
- 跑步：[16_35](https://raw.githubusercontent.com/una-dinosauria/cmu-mocap/master/data/016/16_35.bvh)，参考帧 7–96。
- 跳跃：[118_01](https://raw.githubusercontent.com/una-dinosauria/cmu-mocap/master/data/118/118_01.bvh)，使用其中准备、起跳、下落和落地片段。
- 待机：[16_01](https://raw.githubusercontent.com/una-dinosauria/cmu-mocap/master/data/016/16_01.bvh)，使用起跳前的静立片段。

The data used in this project was obtained from mocap.cs.cmu.edu.
The database was created with funding from NSF EIA-0196217.

原始数据使用说明见 `CMU_BVH_READMEFIRST.txt`。CMU 允许将数据用于项目和商业产品，但不允许直接转售数据。

## 验证与回退

`playmode_validation.json` 记录了通过的 Unity 运行测试：行走、跑步、两种模式切换、停止回待机、跑动跳、原地跳和防二段跳。两种跳跃测试均观察到了实际 Animator 的 Prepare、Jump、Fall、Land 状态。

`animation_quality.json` 记录了实际渲染模型的接地高度和关节采样变化。Jump / Fall 的模型最低点是相对于角色根节点测量的，需加上游戏中的离地位移。

`BackupBeforeRefinement.unitypackage` 是修改前的完整角色与场景备份。需要回退时可通过 Unity 的 Assets > Import Package > Custom Package 导入；导入同名资产会覆盖当前改进版，请仅在决定回退时使用。

# 弹反爆闪 ParryFlash

预制体：`Assets/Characters/Combat/Particles/ParryFlash.prefab`
构建脚本：`Assets/Characters/Combat/Editor/ParryFlashBuilder.cs`
着色器：`ParryFlashAdditive.shader`（URP 粒子 Unlit + 加法混合 `Blend SrcAlpha One`）

菜单：

- `Tools/Combat/Build Parry Flash` —— 生成 / 覆盖预制体
- `Tools/Combat/Preview Parry Flash` —— 在场景视图中心放一份实例，循环播放（每 1.6 秒重来）
- `Tools/Combat/Stop Parry Flash Preview` —— 停止循环

## 两个 Unity 6 的坑（已在脚本里绕开）

- `GetComponent` 找不到组件时返回的是假 null，`??` 判断不出来，必须用 Unity 重载的 `== null`。
- `MainModule.playOnAwake` 在编辑器下用脚本赋值不会被序列化，得用 `SerializedObject`
  直接写 `playOnAwake` 属性；不设的话运行时 Instantiate 出来不会自动播。
- 编辑模式下 Unity 自带的粒子预览面板**不推进 `useUnscaledTime` 的系统**，按 Play 没反应。
  所以 Preview 菜单是自己挂 `EditorApplication.update` 调 `Simulate()` 驱动的，
  不要用 Inspector 里那个 Particles 播放面板判断特效好不好使。

## 七层

| 层 | 贴图 | 数量 | 寿命 | 说明 |
|---|---|---|---|---|
| 01_Core_Flash | ParticleRoundGlow | 1 | 0.13–0.16 | 中心爆闪，HDR 白 ×9 |
| 02_Cross_Beam | ParticleBeamGlow | 2 | 0.11–0.15 | 横向贯穿光带，面片 7–11 × 1.75–2.75 |
| 03_Radial_Spikes | ParticleStreak | 70 + 22 | 0.16–0.30 | 主放射光针，速度 7–26，拉伸渲染 |
| 04_Fine_Rays | ParticleStreak | 110 + 30 | 0.11–0.20 | 次级细针，速度 15–38 |
| 05_Impact_Ring | ParticleImpactRing | 2 | 0.26–0.32 | 冲击环，尺寸扩张到 6 倍 |
| 06_Sparks | ParticleChip | 34 + 12 | 0.35–0.70 | 溅射火星，重力 0.55–0.95 |
| 07_Haze | ParticleSoftGlow | 6 | 0.40–0.62 | 紫色余雾，压底噪 |

全部 `useUnscaledTime = true`，顿帧期间照常播放；`simulationSpace = World`，
角色移动不会拖着特效走。根节点 `stopAction = Destroy`，运行时播完自毁。

## 两张程序化生成的光晕素材

生成脚本的参数都写在下面，改完重新跑一遍脚本再 Build 即可。两张都是 RGBA，
亮度在 alpha 通道，RGB 是核心纯白往外过渡到品红 (1, 0.24, 0.92)。

### ParticleBeamGlow.png —— 横向光带，1024×256（4:1）

六层叠加：

- **核心白芯**，粗细沿长度变化：`core_sigma = 0.026 × (1 + 2.2 × exp(-(x/0.44)²))`，
  中心最粗、往两端收成一根细线。实测 alpha>200 的厚度：中心 32px、3/4 处 8px、末端 0。
  指数用 1.6（次高斯）而不是 2，尾巴更肥，白芯往外过渡到光晕不会突然断开。
- 衔接层 `bloom` σ=0.055，强度 0.50 —— 专门填白芯和紧光晕之间的尺度断层
- 紧光晕 σ=0.15，强度 0.68
- 大柔光 σ=0.46，强度 0.38
- 最外围薄雾 σ=0.70，强度 0.15
- 中心圆形爆点 0.80（按 4:1 宽高比补偿过，画出来才是圆的）
- 长度包络 `exp(-|x/0.86|^2.2)`

### 光晕要更亮更艳

两个独立的旋钮，别混：

- **更亮** = 提高 `tight` / `wide` / `far` 三层的强度（alpha 高 → 加法混合叠得更多）
- **更艳** = 压低 `EDGE` 的绿通道（现在是 `(1.00, 0.13, 0.96)`），
  同时**收紧 whiteness**。白会冲淡颜色，白铺得越远光晕越粉越灰，
  所以 `bloom` 在 whiteness 里的系数从 1.05 降到了 0.45。

贴图里的 RGB 封顶是 1，真正的"过曝发光"来自材质上的 HDR Intensity 和 Bloom，
不要指望在贴图里把颜色画得更亮。

### 两个"别用 clip"的地方

alpha 和 whiteness（白到品红的比例）都不能用 `clip(0,1)` 收口 —— clip 会在饱和区
压出一块平顶，平顶的边界就是一道看得见的硬边。两处都改用 `soften(x, k) = 1 - exp(-k·x)`，
渐近趋近 1 但永远不到，没有边界。

k 控制"多快接近纯白/纯不透明"：**k 越大白区越大**。alpha 用 k=1.05，whiteness 用 k=2.6。
把 clip 换成 soften 时白区会缩水（soften 在饱和区比 clip 保守），系数要相应调高。

另外长度包络的指数别超过 ~2.4：3.2 次方的超高斯顶部平、肩部陡，末端会啃出硬边。

四边用 `fade()` 做 smoothstep 收口（X 从 0.74、Y 从 0.50 开始降到 0），保证 alpha
在贴图边界严格为 0。改完这样自查，四个数必须全是 0：

```python
a = np.asarray(Image.open('ParticleBeamGlow.png').convert('RGBA'))[...,3]
print(a[0].max(), a[-1].max(), a[:,0].max(), a[:,-1].max())
```

另：`save()` 的 margin 参数为 0 时必须跳过那几行切片 —— `a[-0:]` 在 numpy 里
等于整个数组，会把整张图清零，生成出来是纯黑。

**面片比例必须跟贴图一致（4:1）**，光带的"细"是烘在贴图里的，不是靠把面片压薄 ——
压扁面片会把光晕一起压没。光晕要更大就加大画布高度 + 各层 σ，不是放大面片。

### ParticleRoundGlow.png —— 圆形光晕，512×512

五层同心高斯 + 边缘强制归零（`cutoff`，防止贴图边界出现方块状硬边）：

- 白热核心 σ=0.150
- 过渡层 σ=0.26，强度 0.70
- 紧光晕 σ=0.40，强度 0.46
- 大柔光 σ=0.56，强度 0.30
- 外圈薄雾 σ=0.78，强度 0.14

白心要更大就调 `c_core` 的 σ 和 whiteness 里的系数（当前 `c_core × 1.60 + c_mid × 0.60`，k=2.6）。

比原来的 `ParticleAura` 扩散大得多，所以 `01_Core_Flash` 的起始尺寸从 2.2–2.8
收到了 1.8–2.3，否则一炸就糊满屏。

### ParticleSoftGlow.png —— 纯光晕圆，512×512

没有白芯，整张都是品红，中心只是更亮更不透明。四层同心高斯：

- σ=0.30，强度 0.85
- σ=0.46，强度 0.50
- σ=0.64，强度 0.28
- σ=0.85，强度 0.12

whiteness 恒为 0，所以不管多亮都不会泛白。当前用在 `07_Haze`（替掉了 `ParticleWisp`），
想换回去改 builder 里 `hazeMat` 那一行的贴图名即可。也适合拿来做拖尾、蓄力光球、
受击瞬间的底光这类"只要颜色不要形状"的地方。

三张的导入设置都走 `ImportGlowTexture()`：Bilinear + 无 mipmap + Clamp + 不压缩。
注意这跟项目里像素风素材惯用的 Point 过滤是反的，平滑渐变不能用 Point。

## 发光靠的是 Bloom，不是粒子本身

粒子颜色是 HDR（RGB 乘了 3.5–9 倍），加法混合叠上去。**只有开了后处理 Bloom，
超过 1 的部分才会外溢成光晕**，否则会直接被钳到白色，看着只是亮，不发光。

当前工程状态（2026-09）：

- 主相机 `Render Post Processing` = **关**
- 场景里**没有 Volume**，所以只有 `Settings/DefaultVolumeProfile` 生效，
  其中 Bloom `intensity` = **0**
- `Settings/SampleSceneProfile` 的 Bloom 是 threshold 1 / intensity 0.25，但没被场景引用

要看到图里那种辉光，需要：

1. 相机的 URP Additional Camera Data 勾上 Post Processing
2. 场景里加一个 Global Volume，profile 用 SampleSceneProfile（或新建），
   Bloom intensity 调到 0.8–1.5、threshold 约 1.0

注意这会影响整个画面，不只是这个特效。

## 与像素栅格滤镜的冲突

`RedhornPixelFilter` 跑在 AfterRenderingPostProcessing，会把 Bloom 之后的画面量化。
04_Fine_Rays 只有 0.05–0.12 粗，过了栅格容易断裂闪烁。若明显，把 `Fine_Rays`
的 startSize 抬到 0.10–0.18，或在弹反瞬间临时降低栅格强度。

## 与 StylizedRelight 的关系

`StylizedRelightFeature` 注入在 BeforeRenderingTransparents，粒子走 Transparent 队列，
不会被重打光，自发光原样保留。不需要为这个特效改渲染器。

## 调参入口

- 数量 → 各层 Emission / Bursts
- 光针长度 → Renderer / Velocity Scale（长度 = 速度 × 这个值）
- 光针粗细 → Main / Start Size
- 发光强度 → Main / Start Color 的 HDR Intensity
- 颜色 → 同上；当前是品红 (1, 0.3, 0.95) 系，改成别的色系只要动 Start Color
- 扩散范围 → Start Speed 与 Limit Velocity / Dampen

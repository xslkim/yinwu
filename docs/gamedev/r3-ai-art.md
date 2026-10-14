# R3 · AI + 开源素材的美术工作流（2D/3D/UI/动画）

> 调研对象：一人公司（AI 承担全工种）开发 3D 光枪射击游戏，PC 先行、后上 Android 智能电视；一局 3–5 分钟；画面要在抖音直播间"一眼惊艳"；用户在中国大陆。
> 调研日期：2025 年。所有结论标注【已核实】（有官方页面/多方来源佐证）或【估计】（单一来源或推测）。价格以官网为准，本文价格均为调研时点快照。
> 标记 🇨🇳 = 中国大陆可直接访问，无需翻墙；🪜 = 需要翻墙。

---

## 0. 一页结论（先看这里）

**推荐的美术总路线**：`CC0 素材打底（70%）+ AI 生成补齐缺口（20%）+ 手工微调统一风格（10%）`，风格定为 **低多边形（low-poly）+ 卡通渐变贴图 + 描边/Bloom 后处理**。这条路线的现金成本可以压到 **0～约 ¥150/月**，且全部素材可商用。

| 环节 | 首选工具 | 费用 | 国内可用 |
|---|---|---|---|
| 3D 场景/怪物/武器基底 | Quaternius（Toon Shooter Kit、Ultimate Monsters）+ KayKit（Skeletons） | 免费 CC0 | ✅ 官网直连 |
| 动画 | Mixamo（自动绑骨 + 2000+ 动作） | 免费，可商用 | ⚠️ 偶尔不稳定，能直连 |
| 2D/UI/图标 | Kenney（UI Pack、Crosshair Pack、Game Icons） | 免费 CC0 | ✅ 直连 |
| AI 生成 3D（补缺） | Tripo 🇨🇳 / 混元3D 🇨🇳（开源可本地跑） | 免费额度起步 | ✅ |
| AI 生成 2D（贴图/宣传图） | 即梦 AI 🇨🇳 / 本地 ComfyUI+SDXL/Flux | 免费额度 / 完全免费 | ✅ |
| 贴图/PBR/HDRI | Poly Haven + ambientCG | 免费 CC0 | ⚠️ Poly Haven 可直连（有镜像） |
| 天空盒 | Blockade Labs Skybox AI 🪜 / 混元3D 全景 / SDXL 全景 LoRA | 免费额度 | 部分 |

---

## ① 一人做 3D 射击游戏：美术风格怎么选才能成本最低

### 结论：选"低多边形 + 卡通渲染（cel/toon shading）"，不要碰写实

理由（成本维度逐项对比）：

| 风格 | 建模成本 | 贴图成本 | 性能成本（TV 端关键） | AI/素材生态 |
|---|---|---|---|---|
| 写实 PBR | 极高（高模+烘焙+4K贴图） | 极高 | 高，低端电视跑不动 | AI 生成质量仍不够 |
| 手绘贴图中模 | 高 | 高（需手绘功底） | 中 | 一般 |
| **低多边形 + 渐变/顶点色** | **极低**（几百~几千面） | **几乎为零**（一张 1×N 渐变图走天下） | **极低**，十年前的电视芯片都流畅 | **极好**（Quaternius/Kenney/KayKit 全是这个风格） |
| 体素（voxel） | 低 | 低 | 低 | 一般，风格受限 |

【已核实】关键技巧——"一张渐变贴图管所有模型"：低多边形模型的 UV 全部指向同一张小渐变图（gradient/palette texture），改配色 = 换贴图，不需要重新建模。Quaternius、Kenney 的 3D 包全部用此方案。

### 直播间"一眼惊艳"怎么在低多边形下实现

低多边形 ≠ 画面简陋。直播镜头是远距离小窗口，观众记住的是**色彩对比和动态**，不是贴图精度：

1. **高饱和配色 + 强对比**：场景用深底色（深蓝/深紫夜空），敌人/靶子用高饱和亮色（荧光绿、橙红），弹道和命中特效用最亮色。Synty 系低价包的套路。
2. **后处理三件套**（Unity/UE 都有现成免费组件）：Bloom（泛光）、描边（outline shader，卡通感来源）、轻微色差（chromatic aberration）。这三样是"卡通渲染惊艳感"的 90%。
3. **命中反馈堆特效而非模型**：Kenney Particle Pack（CC0）+ 屏幕震动 + 慢动作特写，比高精度模型便宜一百倍、直播效果强十倍。
4. **大色块 UI**：参考《PUBG Mobile》《喷射战士》的直播画面——大字、大图标、高对比血条。

### 成功案例（已核实）

- **Vampire Survivors**（poncle，核心开发 1 人）：像素素材 + 低价素材包起步，Steam 24 万+ 好评，证明"便宜美术 + 强玩法"可以爆。作者公开承认初期直接用现成素材包。[Wikipedia](https://en.wikipedia.org/wiki/Vampire_Survivors)
- **Balatro**（LocalThunk，1 人）：2D 简单美术，2024 年现象级销量。[DevIndieGame 案例分析](https://devindiegame.com/articles/why-roguelikes-are-best-for-indie-games.html)
- **Lethal Company**（Zeekerss，1 人）：故意做 PS1 低保真 3D，销量千万级——"风格化的穷"成为卖点。【已核实为单人开发，销量数据为公开报道】
- 直接用 CC0 素材商用的实证：Photon 官方 [Fusion Starter 示例工程](https://doc.photonengine.com/fusion/v2/game-samples/fusion-starter) 使用 Kenney + KayKit + Quaternius；手游 [City Defense](https://citydefense.app/en/map-data-and-credits/) 的商业产品 credits 页列出 Quaternius Ultimate Monsters / Toon Shooter Game Kit / Sci-Fi Modular Gun Pack + Kenney + KayKit——**和你这个项目的需求几乎一模一样**。

---

## ② 开源/免费素材库逐个盘点

### 授权速查（重要）

- **CC0（公有领域）**：可商用、可改、**无需署名**。最省心，本项目主力。
- **CC-BY**：可商用可改，**必须署名作者**。维护一个 `ATTRIBUTION.md`，游戏内"制作名单"页列出即可。
- **CC-BY-SA**：可商用，但**衍生品必须以相同协议发布**——游戏美术一旦混入 SA 素材，该部分美术可能被要求开源，**建议整体回避**。
- **GPL**：OpenGameArt 上有不少 GPL 素材，传染性强，**回避**。
- **OGA-BY**：OpenGameArt 自定义协议，类似 CC-BY，需署名。

### 2.1 Kenney 🇨🇳 — kenney.nl

- 授权：**全部 CC0**【已核实】。超 50,000 个素材（2D/3D/UI/音频/字体）。
- 射击游戏直接可用的包：
  - **Crosshair Pack**：几百个准星图，光枪游戏必备。【已核实存在】
  - **Particle Pack**：爆炸/烟雾/火花贴图序列，命中特效来源（上文 City Defense 案例即用它）。
  - **Game Icons / Input Prompts**：2000+ UI 图标、手柄/键盘按键提示图。
  - **3D 包**：Space Kit、Graveyard Kit、Tower Defense Kit、Blaster Kit（低模枪械）等。
- 格式：3D 提供 GLB/OBJ/FBX；2D 为 PNG/SVG。
- 价格：全部免费；itch.io 上有一次性付费的 "Kenney All-in-1" 打包下载（约 $9.99，纯打赏性质）。【已核实】
- 国内访问：官网可直连；下载慢可从 GitHub 的 [kenneynl 组织](https://github.com/KenneyNL) 拉取。【估计：GitHub 国内速度不稳，可配镜像】

### 2.2 Quaternius 🇨🇳 — quaternius.com

- 授权：**CC0**，无需署名【已核实，[第三方盘点核实记录](https://tmhsdigital.github.io/Free-Game-Dev-Assets/entry/quaternius/)】。
- **对射击游戏最有价值的包**【已核实存在，见 City Defense credits 与 Photon 文档】：
  - **Toon Shooter Game Kit**：卡通射击主题整套（武器、靶场道具、场景件）——**本项目的首选场景基底**。
  - **Ultimate Monsters**：50+ 只带绑定和动画的低模怪物，直接当敌人。
  - **Zombie Apocalypse Kit**：僵尸+城市场景。
  - **Sci-Fi Modular Gun Pack**：模块化科幻枪械。
  - **Animated Mech Pack / Ultimate RPG Pack**：机甲和 RPG 角色，均带动画。
- 格式：FBX/OBJ/Blend/GLB，多数带骨骼和动画剪辑。
- 价格：免费版免费下载；Patreon 付费版（约 $5/月起）解锁更多包和工程文件，**授权同样是 CC0**。【已核实】

### 2.3 KayKit（Kay Lousberg）🇨🇳 — kaylousberg.com

- 授权：**CC0**【已核实】。
- 关键包：
  - **Character Pack: Skeletons**（骷髅兵，**带骨骼绑定和整套动画**：走/跑/攻击/死亡）——低模怪物直接可用，风格与 Quaternius 兼容。
  - **Adventurers / Dungeon Remastered / Platformer Pack / Game Bits**：奇幻角色、地牢场景、平台件、通用道具。
- 格式：FBX/glTF/OBJ。【已核实】
- 价格：基础包全部免费；itch.io 上有付费"系列合集"，仍是 CC0。

### 2.4 OpenGameArt 🇨🇳 — opengameart.org

- 授权：**逐素材不同**（CC0 / CC-BY / CC-BY-SA / OGA-BY / GPL 混合）【已核实，[DesignBlocs 盘点](https://www.designblocs.com/resources/3D/)】。约 5000 个 3D 条目。
- 用法：**用左侧授权筛选器只勾 CC0 和 CC-BY**；质量参差，适合淘音效、UI、贴图，不建议当主力 3D 来源。
- 维护一份 per-asset 的 credits 表（素材名/作者/协议/URL），上架前自查一遍。

### 2.5 Poly Pizza 🇨🇳 — poly.pizza

- 内容：约 **10,600 个低模模型**，含被关停的 Google Poly 档案的抢救版；全部为独立 .glb 单文件，面数低。【已核实，[MCP for Blender 文档](https://mcp-for-blender.com/docs/integrations/poly-pizza)】
- 授权：**约 69% 为 CC-BY（必须署名），其余 CC0**。搜索时加 `licence="CC0"` 过滤可完全免署名。【已核实】
- 用法：注册拿免费 API key 可直接接 Blender 插件；也可网页下载。
- 适合：补充场景道具（车辆、家具、武器、怪物杂兵）。

### 2.6 Mixamo ⚠️（Adobe，可直连但偶尔抽风）— mixamo.com

- 授权【已核实，[Adobe 官方 FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html)】：凭免费 Adobe ID 即可用；**角色和动画均可免版税用于个人和商业项目**；**禁止把动画/角色作为独立资产转售/再分发**（嵌进游戏里 OK）。
- ⚠️ 注意：网上有第三方声称"商用需 Creative Cloud 订阅"，与 Adobe 官方 FAQ 冲突；以官方 FAQ 为准，但建议下载时截图留存授权页。
- 功能：上传自己的人形模型（FBX/OBJ）→ 自动打骨骼 → 套用 2000+ 动作库（走跑/攻击/受击/死亡/跳舞）→ 导出 FBX。
- 本项目用法：Quaternius/KayKit 的人形怪如果缺某个动作，或者 AI 生成的人形模型，都丢进 Mixamo 自动绑骨。**这是"动画"工种最便宜的一整条流水线。**
- 国内访问：mixamo.com 可直连（Adobe 国内 CDN），但登录偶尔慢；备选方案见 2.7。

### 2.7 补充库（都值得收藏）

| 站点 | 内容 | 授权 | 国内 |
|---|---|---|---|
| [Poly Haven](https://polyhaven.com) | 顶级 CC0 PBR 贴图/HDRI/模型 | CC0 | ⚠️ 可直连，慢 |
| [ambientCG](https://ambientcg.com) | 2000+ CC0 PBR 无缝贴图 | CC0 | ✅ |
| [Sketchfab](https://sketchfab.com) | 最大 3D 社区，按 "Downloadable + CC0/CC-BY" 筛选 | 混合 | ⚠️ |
| [itch.io 素材区](https://itch.io/game-assets) | 大量免费/低价包，注意逐个看授权 | 混合 | ✅ |
| [CMU Mocap 数据库](http://mocap.cs.cmu.edu/) | 2500+ 真人动捕 BVH，公有领域 | 公有领域 | ⚠️ |
| 100 Avatars R1+R2（GitHub） | 167 个 Mixamo 兼容骨骼的人形 | CC0 | ✅ GitHub |

---

## ③ AI 生成 3D 模型的现状（2025 年快照）

### 总体判断【已核实多方评测】

AI 3D 已经能做"道具/怪物白模 + 自动贴图"，**但输出的网格是密集三角面（4 万～12 万面），拓扑杂乱、无骨骼、UV 由 AI 自动展开**。直接进引擎**用于静态道具/靶子/场景摆件没问题**；用于需要动画的角色，必须走"AI 生成 → 自动重拓扑 → Mixamo 自动绑骨"流水线。实测对比（同 prompt "low-poly fantasy sword"）：Rodin 120k 面细节最锐但要重拓扑；Meshy 60k 面风格最贴游戏；Tripo 40k 面最快（35 秒）但细节糊。[FlowPix 横评](https://www.flowpixai.com/ai-tools/ai-3d-model-generators-meshy-triposr.html)

### 逐家盘点

**Tripo（VAST/深言科技系，🇨🇳 国内直连）** — tripo3d.ai / tripo3d.cn
- 免费额度：注册送积分 + 每月免费额度（来源说法 200–300 积分/月，约可生成 7–13 个模型）；API 新用户送 2000 积分（≈$20）。【已核实，不同月份有波动：[kingy.ai 评测](https://kingy.ai/news/tripo-ai-the-all-in-one-ai-3d-workspace-that-might-actually-change-how-we-build-3d-content/)、[aicredits.dev](https://aicredits.dev/submissions/190-tripo-ai-2-000-free-api-credits-on-signup-300-month-on-basic-plan)】
- 付费：Professional $19.90/月（年付约 $11.94/月）；API 按量 $0.01/积分，文生 3D 约 30 积分/次（≈$0.3/模型）。[Tripo 开发者定价页](https://developers.tripo3d.ai/en/pricing)
- 授权 ⚠️：免费版输出**公开可见**，授权条款各来源说法不一（一说 CC-BY 4.0 署名可商用，一说免费版仅非商用），**商用前请以官网 Terms 当前文本为准；付费版解锁私有 + 完整商用**。【存在冲突，已标注】
- 导出：GLB/OBJ/FBX/STL/USDZ，支持自动减面、转四边面、PBR 贴图。
- **国内背景**：VAST 是中国公司，有中文站和中文客服，支付方便——**国内用户首选**。

**腾讯混元3D（Hunyuan3D，🇨🇳 国内直连 + 开源可本地部署）** — 3d.hunyuan.tencent.com / GitHub Tencent-Hunyuan/Hunyuan3D-2
- 【已核实】1.0/2.0/2.1 已开源，权重+推理代码在 HuggingFace/GitHub 免费下载，**官方宣布免费可商用**（[新浪科技报道](https://finance.sina.cn/stock/jdts/2025-09-27/detail-infrxeuv4076537.d.html)、[IT之家](https://www.ithome.com/0/886/122.htm)）。
- ⚠️ 授权注意点：腾讯 Hunyuan 社区协议对**月活超 1 亿的大公司**和部分欧盟/英国/韩国司法区有限制条款；对一人公司商用**无影响**，但上架前建议通读 LICENSE 文本。【已核实协议存在此类条款】
- 硬件要求：本地跑需 10–29GB 显存 GPU；无 GPU 就用网页版**混元3D Studio 免费额度**。
- 能力：文生 3D / 图生 3D / 多视图 / PBR 贴图 / 2025 年新增 Hunyuan3D-Part 组件级拆分（可把生成的整体模型拆成 50+ 独立部件，利于做可破坏靶子）。【已核实，IT之家】
- 质量：几何精度业界第一梯队，贴图风格偏写实，配低模卡通风格需要在 prompt 里强调 "low-poly, flat colors, game asset"。

**Meshy ⚠️（国内访问情况未完全核实，建议实测 meshy.ai；有迹象可直连）** — meshy.ai
- 免费：每月约 200 积分（约 5–20 个模型，视模式）；**免费版输出为 CC-BY 4.0——署名即可商用**【已核实，[figmee 对比](https://figmee.me/zh-CN/blog/image-to-3d-ai-tools-comparison)】。
- 付费：Pro $20/月（年付约 $16/月）、Max $60/月，付费版解锁私有和完整商用授权。[Meshy 定价综述](https://note.com/th1980/n/nd85232f8d6d3?hl=en)
- 特色：Text to Texture（给已有模型 AI 重贴图，**对统一 CC0 素材风格极有用**）、自动重拓扑（转四边面）、自动绑骨+简单动画。
- 质量：风格化游戏资产做得最好（见上方 FlowPix 横评）。

**Rodin / Hyper3D（影眸科技，上海，🇨🇳 国内直连）** — hyper3d.ai
- 【已核实】2026-01 发布 Rodin Gen-2 Edit：生成+提示词局部编辑一体化。[DoNews](https://www.donews.com/news/detail/8/6401434.html)
- 价格：免费体验额度；付费三档——教育 €15/月、创作者 €30/月、**商用 €120/月**（积分制）。**商用授权需要付费档**。[hyper3d.ai/pricing](https://hyper3d.ai/zh/pricing)
- 质量：几何细节最锐利（横评第一），但面数高，进引擎前必须减面/重拓扑。

**Luma Genie 🪜** — lumalabs.ai/genie
- 文本生 3D 免费额度仍在，但 Luma 公司重心已转向 Dream Machine 视频模型，Genie 更新放缓；质量在横评中落后于 Meshy/Tripo。网站需翻墙。【已核实存在，质量评价为估计】**国内不建议为它翻墙，直接用 Tripo/混元替代。**

### "能不能直接进引擎"操作结论

| 用途 | 能否直接用 | 需要的处理 |
|---|---|---|
| 静态场景摆件/靶子/武器皮肤 | ✅ 可以 | 导入后减面（Blender Decimate 或引擎 LOD），缩放校正 |
| 需要形变的角色/怪物 | ❌ 不行 | 自动重拓扑（Meshy 内置 / Blender Quad Remesher 付费 / Instant Meshes 免费开源）→ Mixamo 自动绑骨 → 套动画 |
| 手机/电视低端平台 | ⚠️ | AI 模型动辄 10 万面，必须减到 5k–20k 面以下 |

---

## ④ AI 生成 2D：UI、贴图、宣传图

### 本地方案 vs 在线方案

| 方案 | 费用 | 国内可用 | 适合 |
|---|---|---|---|
| **ComfyUI + SDXL/Flux（本地）** | 免费无限量（需 8GB+ 显存） | ✅ | 批量贴图、风格统一训练 LoRA、不想花钱时 |
| **即梦 AI（字节）🇨🇳** jimeng.jianying.com | 每日免费约 60 积分；会员约 ¥69/月起 | ✅ | 宣传图、海报、直播封面、概念图 |
| **可灵 AI（快手）🇨🇳** | 每日免费额度；会员约 ¥66/月 | ✅ | 图生视频（直播预告片） |
| **通义万相（阿里）🇨🇳** | 免费额度 | ✅ | 备用 |
| **LiblibAI 🇨🇳** | 按点数，有免费额度 | ✅ | 在线跑 SD 模型，不装环境 |
| **Midjourney 🪜** | $10/月起，无免费额度 | ❌ 需翻墙 | 美学天花板；国内替代=**悠船（MJ 官方中文版，桌面客户端直连）** |
| DALL-E 3 / GPT 生图 🪜 | $20/月 | ❌ | 含文字的海报渲染准确 |

【已核实：即梦无需翻墙、每日免费额度、约 ¥59–69/月档；Midjourney 需翻墙无免费档；悠船为 MJ 官方中文渠道。来源：[掘金对比](https://juejin.cn/post/7598382710294740992)、[AI万址悠船评测](https://ai-wz.com/youchuan)、[IT码农对比](https://tanqingbo.cn/ai-image-generation-guide/)】

### 分场景工作流（可直接照做）

**1. 无缝贴图（地面/墙面/水面，tileable）**
- 本地方案：ComfyUI + SDXL，加载 "seamless" 类 LoRA 或启用 circular padding；生成后用 **GIMP/Photoshop 的 Offset 滤镜（位移 50%）检查接缝**，接缝处用修补工具或 inpainting 修掉。
- 偷懒方案：直接去 ambientCG / Poly Haven 下载现成 CC0 无缝 PBR 贴图（几千张，大概率够用）。
- 低模卡通风格下其实**不需要无缝贴图**：纯色/渐变 + 顶点色即可（见①）。

**2. 天空盒 / 全景背景**
- 🪜 [Blockade Labs Skybox AI](https://skybox.blockadelabs.com)：文本直接生成 equirectangular 360° 全景图，免费额度，导出直接当天空盒。需翻墙。
- 不翻墙方案：① 即梦/通义万相生图时 prompt 加 "equirectangular projection, 360 panorama, seamless"；② 本地 ComfyUI 有全景 LoRA；③ Poly Haven 免费下载真实 HDRI（CC0）。【估计：AI 直接生成的全景在接缝处需手动修】

**3. UI 图标 / 道具图标**
- 第一选择不是 AI：**Kenney Game Icons（2000+ 枚 CC0）** 直接够用且风格统一。
- AI 方案：即梦/SDXL prompt 模板："game UI icon, [子弹/金币/血量], flat vector style, bold outline, dark background, single object" → 生成后用 remove.bg（或本地 rembg，开源）抠背景 → 导入 Unity/UE 的 UI 图集。
- 风格统一技巧：同一批图标用同一个 seed/同一个 LoRA 批量生成。

**4. 宣传图 / 直播封面 / 商店图**
- 即梦 AI（中文 prompt 原生支持，出 2K/4K 图），或 ComfyUI + SDXL 配合游戏截图做 img2img 增强。
- 大字标题不要交给 AI：AI 生成底图 → 用剪映/Photopea（免费在线 PS，🇨🇳 可直连）加文字。

**5. 给 CC0 低模"换皮"统一风格**
- Meshy 的 Text to Texture 或混元3D 的贴图重绘：把 Quaternius 素模传进去，prompt 指定配色方案，让全部素材看起来是"同一款游戏"。【估计：此流程对低模有效，需逐个试】

---

## ⑤ 中国大陆可直接使用清单（汇总）

| 需求 | 国内直连首选 | 备选 |
|---|---|---|
| AI 3D 生成 | **Tripo（tripo3d.cn）**、**腾讯混元3D** | Rodin（hyper3d.ai） |
| AI 3D 本地开源 | **Hunyuan3D-2.1（GitHub/HF，镜像站 hf-mirror.com）** | TripoSR（Stability 开源轻量版） |
| AI 2D 图片 | **即梦 AI**、可灵、通义万相 | LiblibAI（在线 SD）、本地 ComfyUI |
| MJ 平替 | **悠船（官方中文版）** | 即梦 |
| 素材库 | Kenney / Quaternius / KayKit / OpenGameArt / Poly Pizza 全部可直连 | itch.io |
| 抠图 | rembg（开源本地） | remove.bg 🪜 |
| 图片处理 | Photopea（在线 PS） | GIMP（开源本地） |
| HuggingFace 下载 | **hf-mirror.com 镜像** | 各模型国内网盘搬运 |

需翻墙（不建议为它们折腾）：Midjourney 原版、Luma Genie、Blockade Skybox、Adobe Firefly。每项都有上面的国内替代。

---

## ⑥ 落地步骤级流水线（照做版）

**第 1 周：素材基底（0 元）**
1. 注册并下载：Quaternius 的 Toon Shooter Game Kit + Ultimate Monsters + Sci-Fi Modular Gun Pack；KayKit Skeletons；Kenney 的 Crosshair Pack + Particle Pack + Game Icons + UI Pack。
2. 全部导入引擎，建一个 `ATTRIBUTION.md`（即便 CC0 不强制，养成习惯；Poly Pizza 的 CC-BY 素材必须记）。
3. 统一风格：给所有模型挂同一个 toon shader + Bloom + 描边后处理，截一张场景图，这就是你的"视觉锚点"。

**第 2 周：动画（0 元）**
4. 人形怪缺动作 → 传 Mixamo 自动绑骨 → 下载 walk/attack/hit/death 四个剪辑 → 引擎里建动画状态机。
5. 骷髅兵直接用 KayKit 自带动画。

**第 3 周：AI 补缺（0～¥150）**
6. 列出 CC0 库里找不到的资产清单（比如"特定的 BOSS 怪、主题场景摆件"）。
7. 用 Tripo 免费额度逐个生成（prompt 模板：`low-poly stylized game asset, [物体], flat colors, clean silhouette`），导出 GLB。
8. 静态物：Blender Decimate 减面到 2 万面以下 → 进引擎。角色：重拓扑 → Mixamo。
9. 风格不统一：用 Meshy 免费额度的 Text to Texture 重贴皮，或干脆全部换成"渐变贴图 + 描边"方案，AI 只出白模。

**持续：2D/UI/营销（0 元起）**
10. UI：Kenney 图标 + 引擎 UI 系统先做完整版式。
11. 宣传图/直播封面：即梦 AI 每日免费额度生成底图 + Photopea 加字。
12. 直播素材：可灵图生视频做预告短片。

**上架前：授权自查**
13. 过一遍 `ATTRIBUTION.md`：CC0 随意；CC-BY 在游戏 credits 页署名；确认没有混入 CC-BY-SA/GPL；Tripo/Meshy 免费版输出的署名要求按当时官网条款执行（Meshy 免费版 CC-BY 需署名）。
14. 混元3D 若用开源版本地生成，确认遵守腾讯 Hunyuan 社区协议（对一人公司无实质限制）。

---

## 链接清单

素材库：
- Kenney — https://kenney.nl （GitHub: https://github.com/KenneyNL）
- Quaternius — https://quaternius.com
- KayKit — https://kaylousberg.com
- OpenGameArt — https://opengameart.org
- Poly Pizza — https://poly.pizza
- Mixamo — https://www.mixamo.com （授权 FAQ: https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html）
- Poly Haven — https://polyhaven.com ｜ ambientCG — https://ambientcg.com
- Sketchfab — https://sketchfab.com ｜ itch.io 素材 — https://itch.io/game-assets
- CMU Mocap — http://mocap.cs.cmu.edu/

AI 3D：
- Tripo — https://www.tripo3d.ai （API 定价: https://developers.tripo3d.ai/en/pricing）
- 混元3D — GitHub: https://github.com/Tencent-Hunyuan/Hunyuan3D-2 （HF 镜像: https://hf-mirror.com）
- Meshy — https://www.meshy.ai
- Rodin/Hyper3D — https://hyper3d.ai/zh/pricing
- Luma Genie 🪜 — https://lumalabs.ai/genie

AI 2D：
- 即梦 — https://jimeng.jianying.com ｜ 可灵 — https://klingai.kuaishou.com ｜ 通义万相 — https://tongyi.aliyun.com/wanxiang
- LiblibAI — https://www.liblib.art ｜ 悠船（MJ 官方中文）— https://www.youchuan.ai
- Midjourney 🪜 — https://www.midjourney.com ｜ Skybox AI 🪜 — https://skybox.blockadelabs.com
- ComfyUI — https://github.com/comfyanonymous/ComfyUI ｜ rembg — https://github.com/danielgatis/rembg ｜ Photopea — https://www.photopea.com

参考评测：
- [FlowPix：Meshy vs Tripo vs Rodin 实测横评](https://www.flowpixai.com/ai-tools/ai-3d-model-generators-meshy-triposr.html)
- [figmee：图生3D工具对比（含授权条款）](https://figmee.me/zh-CN/blog/image-to-3d-ai-tools-comparison)
- [Photon Fusion Starter 的第三方 CC0 素材清单](https://doc.photonengine.com/fusion/v2/game-samples/fusion-starter)
- [City Defense 商业游戏的 CC0 素材 credits 页](https://citydefense.app/en/map-data-and-credits/)

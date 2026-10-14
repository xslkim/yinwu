# R2 调研：用 AI 做游戏策划（玩法、关卡、数值）的具体工作流

> 调研日期：2026 年（基于撰写时检索结果）。所有「价格」均为公开网页信息，标注定价页面日期；
> 「已核实」= 有直接来源页面佐证；「估计/推测」= 来源为第三方转述或我基于常识的推断。
> 面向场景：一人公司、3D 光枪射击、一局 3–5 分钟、手机当光枪（UDP 坐标流 + HTTP 事件）、
> 抖音直播演示 + 蓝牙枪套装绑定销售。

---

## 一、成功街机光枪游戏的设计规律

### 1.1 Time Crisis（化解危机）：掩护-探头循环 + 倒计时压迫

来源：[BFI：Time Crisis at 30](https://www.bfi.org.uk/features/time-crisis-30-how-peekaboo-with-guns-put-cabinet-gaming-back-target)（已核实，2025-12 发表）。

可直接照抄的设计规律：

- **核心节奏单位是"一屏"（一个遭遇战）**，不是一关。每屏开始喊一声 "Action!"，然后倒计时开始走，归零扣血。清完一屏敌人=加秒数。**"射击→躲避→装弹→再瞄准"构成一个几秒的呼吸循环**，玩家自己会进入心流节拍。
- **踏板掩护机制给了玩家"主动控制节奏"的权利**：踩下踏板=探头射击（危险），松开=缩回掩体（安全，但时间照走）。这天然制造了"紧张/缓解"交替，且由玩家自己驱动——这是它比纯轨道射击（Virtua Cop）更高明的地方。
- **敌人会趁你缩回掩体时换位置**，迫使你不能无限龟缩，保证每屏都有最低张力。
- 对我们的启发（估计/推测）：手机光枪没有踏板，可用等价物替代掩护节奏——例如"甩枪下压/侧倾手机=换弹兼躲避窗口"、或"敌人开火前 0.5s 有红光预警，此时击中=打断"。

### 1.2 Point Blank：微关卡串烧 = 3–5 分钟局的最佳模板

来源：[IGN 1998 年评测](https://www.ign.com/articles/1998/04/13/point-blank-4)、[GameSpot Point Blank 3 试玩](https://www.gamespot.com/articles/point-blank-3-hands-on/1100-2683243/)、[GamePlaza 博物馆页](https://gameplaza.ch/exhibition/arcade-cabinet/point-blank/?lang=en)（均已核实）。

设计规律：

- **一局由几十个 5–20 秒的微关卡（stage）串成**，关卡之间几乎零加载、零废话。IGN 原文评价其"incredibly fast paced, moving from event to event with little load time"。
- **微关卡按"规则类型"分类**而非按场景：打指定目标并躲避其他目标、一弹一命（One Shot One Kill）、保护 NPC（Rescue）、限时打满 N 个、记忆类、恶搞类（打杂耍球进门）。Point Blank 3 有"射纸板黑手党、骷髅鱼、彩色鸟、体育器材"等大量主题变体——**同一套瞄准机制 + 规则换皮 = 低成本海量内容**，非常适合一人团队。
- **简单控制 + 即时可读 + 快速重开**是街机公共场景（围观、排队、直播）的三要素（GamePlaza 语），恰好也是抖音直播间的三要素。
- 对我们的启发：3–5 分钟一局 = 8–12 个微关卡 × 20–30 秒，每个微关卡只改一条规则（目标/禁射/时限/保护对象），是工作量最低、直播观感最好的结构（估计/推测，但 Point Blank 30 年验证有效）。

### 1.3 House of the Dead：弱点、人质、分支路线与命中率经济

来源：[Wikipedia 系列页](https://en.wikipedia.org/wiki/The_House_of_the_Dead)（已核实）；动态难度见下。

设计规律（社区共识+官方手册可查证的部分标已核实，其余为估计）：

- **Boss 有可视化弱点**（第一代每个 Boss 登场时直接显示弱点图鉴），把"打 Boss"变成解谜式瞄准，而非DPS木桩。
- **人质/平民误伤惩罚**：画面里混入不可打目标，逼迫"识别→决策→开火"，而不只是反应速度。
- **分支路线**：特定事件（救人成功与否）改变后续路线，给重复游玩理由。
- **命中率/连击计分**：高分追求是街机复玩的核心动力。
- **动态难度（DDA）**：街机普遍用橡皮筋式难度调节保证"新手能赚、高手有挑战"，参见 [GameDeveloper：The Secrets of Dynamic Difficulty Adjustment](https://www.gamedeveloper.com/design/more-than-meets-the-eye-the-secrets-of-dynamic-difficulty-adjustment)（已核实，2019）。对直播场景的建议（推测）：宁可做成"显性难度档位+命中率补偿"也不要隐性橡皮筋，因为直播观众要能看懂"主播厉害"，隐性 DDA 会让高手操作显得没有价值。

### 1.4 节奏设计通用理论（可直接套用的工具）

- [The Level Design Book：Pacing 章节](https://book.leveldesignbook.com/process/preproduction/pacing)（已核实）：节奏表只在做前期计划时有用，**"文档永远打不过实机测试"**——和下面 AI 工作流的结论完全一致。
- [GameDesign.gg：Pacing in Level Design](https://gamedesign.gg/articles/pacing-in-level-design/)（已核实）：紧张-缓解（tension/relief）交替、休息点由玩家自己控制（魂系篝火模型）→ 对应光枪游戏里就是 Time Crisis 的踏板。
- 张力的工程学配方（[salivity 的关卡节奏文章](https://salivity.github.io/game-development/article/level-design-architectural-pacing-tension-and-relief)，已核实）：紧张区=尖锐/不对称几何+高对比/闪烁光；缓解区=明亮、对称、开阔。**直播间"一眼惊艳"往往就发生在缓解区**（大场面展示），紧张区负责留存观看。

### 1.5 访问性备注

Reddit/YouTube 上的英文社区分析（如 r/lightgun、各路设计向视频）在中国大陆**需要翻墙**；国内替代：B 站搜"化解危机 设计""死亡之屋 回顾"、知乎游戏设计专栏、游戏机实用技术（UCG）旧文。本节核心结论不依赖墙内不可得的信息。

---

## 二、用 LLM 辅助策划的实战方法（已有完整公开案例）

### 2.1 已核实的完整 AI 策划工作流案例（两个，都是国内团队、中文资料、无需翻墙）

**案例 A：腾讯魔方工作室用 CodeBuddy + PromptX 组建"AI 策划团队"，2 天做出《杀戮尖塔》式肉鸽原型**
来源：[腾讯游戏学堂原文](http://gameinstitute.qq.com/news/detail/321)（已核实，全文细读）。

可直接照做的流程（六阶段）：

1. **"招聘"AI 策划团队**：用 PromptX 插件给每个 AI 角色建立持久化档案（Markdown：职位、职责、设计理念、决策原则）。该项目只保留 4 个角色：**主策划**（统筹收束）、**玩法策划**（核心循环/机制）、**系统策划**（规则/界面结构）、**文案策划**（世界观/描述）。
   - 关键经验①：**数值策划没招——因为"数值策划 AI 做不好"**（原文明确结论）。
   - 关键经验②：必须明确说"**安装 PromptX 插件**"，只说"使用 PromptX"有些工具会假装用了实际没装。
2. **AI 自主讨论**：一句指令"召集策划团队讨论，产出可实现的简化版策划案"，AI 自动拆出 12 个工作流；**每阶段完成后向人类确认再继续**——AI 讨论，人拍板。
3. **先纸面策划、后写代码**：所有规则/卡牌/敌人/事件先落 Markdown 文档（GDD、核心决策、数据表），确认后再进原型。否则"策划改一层、代码跟着改一层"会让 AI 陷入死循环。这条对我们项目极其重要。
4. **Vibe Coding 出可玩原型**。
5. **Debug 阶段人必须深度参与**：原文结论——"AI 在策划案阶段几乎有统治力，但到 Debug 阶段人依然必须参与；对结果负责的永远是做游戏的人。"
6. **美术升级**放最后。
   - 另一坑：遗物/道具的创意文案如果只给笼统要求，AI 会写出与设定"风马牛不相及"的内容——**创意向输出要给足约束和上下文**。

**案例 B：腾讯云架构师林哲《基于 AI Agent 的小游戏开发实践》**
来源：[GameLook 报道全文](http://www.gamelook.com.cn/2026/06/595411/)（已核实，全文细读）。

可直接照做的点：

- **用"专家"对话补齐策划知识盲区**：先做横版射击，但没经验→与射击品类专家 Agent 多轮聊"节奏、难度定位、美术风格"，迭代出完整策划案（世界观/关卡/操作/武器/怪物/UI），不满意就多轮迭代。
- **先用 WebGame（HTML/JS）做玩法验证原型，再移植引擎**：Web 技术栈大模型知识最全、生成质量最稳，浏览器即开即玩；验证通过后再让 AI 导出策划文档、数值配表、关卡数据、美术资产到 Cocos/Unity 工程。**两天完成整个流程**。
- **关卡修改的可视化技巧**：关卡数据类型多，光靠语言说不清改哪→**让 AI 生成一个"关卡可视化 Web 页面"，把人物/怪物/道具/平台全部编号显示**，之后就可以说"把 7 号怪物血量减半、删掉 12 号跳板"。这是单人团队维护关卡数据的实用套路，可直接复用到光枪游戏的波次/靶位编辑。
- 用到的产品：腾讯云 WorkBuddy（专家中心/多专家工作流）+ CodeBuddy（AI IDE，支持 Unity/Cocos/UE 的 MCP 直接操作引擎）。

### 2.2 提示词套路（英文社区验证过的，附来源）

来源：[Summer Engine：AI Tools for Game Design 2026 指南（中文页）](https://www.summerengine.com/zh/blog/ai-tools-for-game-design)（已核实，全文细读；注意该文有推广自家引擎的倾向，但其提示词方法论与腾讯案例互相印证）。

四条核心套路，附可直接复制的提示词模板：

1. **脑暴：给约束，不给开放式问题**
   - 反例："给我一些游戏创意" → 得到平庸答案。
   - 正例模板：`给我10个光枪射击游戏的核心循环，每个一句话，要求：单人3-5分钟一局、一人团队一个月内能做完、适配手机陀螺仪瞄准（精度有限，目标不能太小）、不要丧尸题材。`
   - 压力测试模板：`针对第3个方案，列出它最可能失败的5个原因；再列出Steam上已有的3款做过同样事情的游戏及其差异。`
2. **机制/系统：强制输出规格文档和数据表，不要散文**
   - 模板：`为一款3-5分钟一局的光枪游戏设计计分系统。输出一张表：每种行为（命中/爆头/连击/误伤人质/超时）的得分、触发条件、风险-收益取舍。然后列出3种会惩罚"无脑乱射"的玩家行为的规则。`
   - 找漏洞模板：`这套规则会奖励什么极端策略？玩家会怎么钻空子？`
3. **数值：让 AI 做数学，你来判断好不好玩**
   - 模板：`设计一条12个微关卡的难度曲线。输出CSV表格：关卡号、目标出现间隔秒数、目标在屏存活秒数、同时最大目标数、允许误伤次数、预期命中率。约束：第1关教学（命中率≥90%也能过），第6关小高峰，第12关Boss；整体呈波浪形上升，给出每个数字的推理说明。`
   - **铁律（Summer Engine 原文）：可靠的循环是"AI 生成数值表 → 放进真实构建里玩 → 把结果喂回模型调参"。纯纸面平衡撑不过第一次实测。** 与腾讯案例"数值策划 AI 做不好"的结论一致——AI 做数学推导，人做手感判断。
4. **审查而不是代写**：AI 压缩的是"测试之前的每个阶段"，**永远压缩不了测试本身**。

其他参考：
- [Prompt AI Learning：游戏设计提示词合集](https://promptailearning.com/prompts/gaming-game-design)（模板库，需翻墙程度：估计可直连，未验证）。
- 学术佐证：ChatGPT 作为桌游设计"创意伙伴"的研究 [semanticscholar PDF](https://pdfs.semanticscholar.org/b94a/bc45c8b876d3dba2490de0d5067fb228fe45.pdf)（已核实存在，结论：LLM 适合发散和给反馈，不适合替你做品味判断）。

### 2.3 模型/工具选型与价格（中国大陆可用性）

| 工具 | 用途 | 价格 | 国内直连 | 备注 |
|---|---|---|---|---|
| DeepSeek（网页/App）| 脑暴、规则表、数值表 | **免费**（chat.deepseek.com）| ✅ | 策划案阶段完全够用；V3 API 约 ¥2/百万输入、¥8/百万输出（第三方汇总 [bumo.cc](https://bumo.cc/blog/llm-api-price-comparison-2025-deepseek-qwen-doubao-kimi)，估计，以官方为准）|
| Kimi（Moonshot）| 长文档（竞品评论批量分析）| 网页免费；K2 API 输入约 ¥4–12/百万（估计，见上来源）| ✅ | 长上下文适合一次塞几百条评论 |
| 豆包（字节）| 脑暴、抖音生态内容理解 | 网页/App 免费；API Pro 档输入约 ¥0.8–6/百万（估计）| ✅ | 分析抖音评论语境有主场优势 |
| 通义千问 Qwen | 备用 | 网页免费；API Turbo ¥2/6 元档（估计）| ✅ | 阿里云百炼 |
| Claude Pro | 策划案精修、长文档 | **$20/月，需翻墙+海外支付** | ❌ | 国内替代：DeepSeek/Kimi 长上下文 |
| ChatGPT Plus | 同上 | $20/月，需翻墙 | ❌ | 同上 |
| 腾讯 CodeBuddy | AI IDE、多 Agent 策划 | 有免费额度（以官网为准，估计）| ✅ | 案例 A/B 同款 |
| PromptX | 多 Agent 角色持久化插件 | 开源（GitHub，MIT 类协议，估计，以仓库为准）| ✅ | 案例 A 同款 |
| Cursor / Claude Code | Vibe Coding | $20/月档，需海外支付 | ❌ | 国内替代：CodeBuddy、Trae（字节，有免费版）|

建议（针对本项目）：**策划阶段用 DeepSeek 网页版（免费、直连）跑通 2.2 节的提示词流程；数值表输出 CSV 直接落盘；原型用 CodeBuddy/Trae。只有觉得国产模型"文案品味不够"时再考虑翻墙用 Claude。**

---

## 三、3–5 分钟一局的结构设计模板

### 3.1 通用骨架（综合 Point Blank 微关卡结构 + 街机节奏理论 + 手游会话设计，部分为估计/推测，给出理由）

**模板 A：微关卡串烧式（推荐，Point Blank 血统，最适合直播）**

一局 4 分钟 = 10 个微关卡 × 约 22 秒 + 开场/结算：

| 时段 | 节拍 | 设计意图 |
|---|---|---|
| 0:00–0:10 | 开场钩子：大场面+一句规则 | 直播间前 3 秒决定划不划走，**第一帧就要"一眼惊艳"** |
| 0:10–0:35 | 教学关（命中率≥90% 也能过）| 让新手和直播观众立刻看懂规则 |
| 0:35–1:30 | 3 个微关卡，规则逐个换皮，难度波浪上升 | 建立"什么规则都可能来"的期待 |
| 1:30–1:50 | 缓解+表演关（如一口气打碎 20 个玻璃瓶）| 紧张/缓解交替；给观众截图点 |
| 1:50–2:50 | 3 个高压关：禁射目标、保护 NPC、一弹一命 | 失误惩罚开始显形，弹幕有梗 |
| 2:50–3:30 | 中 Boss 或高潮关（弱点机制、倒计时）| 全局张力峰值 |
| 3:30–4:00 | 结算：得分、命中率、连击、评级、再来一局按钮 | **结算画面是直播转化的关键**（观众想"我上也行"）|

**模板 B：Time Crisis 血统的一屏一清式**（适合剧情化、套装枪卖相）
每屏一个遭遇战：Action! → 倒计时 → 掩护/探头循环 → 清屏加秒。3–5 分钟 = 6–8 屏 + 2 个 Boss 屏。掩护动作用手机姿态模拟。

**模板 C：无尽波次+倒计时**（最容易做，适合先试玩验证）：90 秒基础时间，击杀加时，分数榜驱动复玩。可作为第一个可玩版本。

设计约束清单（照此自查）：
- 单局时长硬上限 5 分钟；每个微关卡 ≤30 秒，规则一句话能说明白（直播间观众中途进来也能看懂）。
- 难度曲线波浪形而非线性（参考模板 A 的缓解点位置）。
- 每个微关卡只换 1 个变量（目标类型/禁射/时限/保护对象/移动速度），其余复用——一人团队的内容生产效率核心。
- 始终有"再来一局"的一键重开（<2 秒进局）。
- 会话设计参考：[GameAnalytics 核心循环指南](https://www.gameanalytics.com/blog/how-to-perfect-your-games-core-loop)、[Game-Ace：街机核心循环 "read-act-react-score" 亚秒级循环](https://game-ace.com/blog/arcade-game-design/)（均已核实）。

### 3.2 把模板变成可执行数值的步骤

1. 把上表丢给 DeepSeek，用 2.2 节数值提示词模板生成 12 关 CSV（出现间隔/存活时间/同时目标数/误伤容忍）。
2. 在引擎里做一个"关卡=CSV 一行"的读取器（配表驱动，不硬编码）。
3. 实机打 10 局，记录自己每关实际命中率，把数据粘回给 AI：`这是我实测命中率：[贴数据]。第4、7关实测比设计难度偏高，保持曲线形状，调整这两关参数，重新输出CSV。`
4. 重复 2–3 轮。**不要试图让 AI 一次给对数值**（腾讯案例 + Summer Engine 双重验证的结论）。

---

## 四、从竞品评论提炼玩家真实需求

### 4.1 Steam 评测挖掘（方法论已核实，有论文和工程博客支撑）

- 方法框架：[Flares：How to Analyze Competitor Reviews（500 条实测方法）](https://www.flares.tech/sources/competitor-reviews)（已核实）——核心流程：① 定语料范围（哪几款竞品、时间窗、每款多少条）；② **把每条评论编码到命名主题**而不是凭感觉读；③ 设**重要性阈值**（提及次数低于阈值的主题不升级为结论）；④ 跨平台交叉验证；⑤ 区分"某竞品独有问题"与"品类通病"。
- 工程实现：[AWS 官博：用生成式 AI 分析游戏评论](https://aws.amazon.com/blogs/gametech/using-generative-ai-to-analyze-game-reviews-from-players-and-press/)（已核实）：检索相关评论 → 喂给 LLM → 生成摘要反馈。
- 学术方法：[LLM-Cure 论文（arXiv 2409.15724）](https://arxiv.org/pdf/2409.15724)：LLM 做竞品评论的"特性改进建议"抽取。
- 现成工具参考：[Reddit r/gamedev：有人做了 Steam 竞品评论 AI 挖掘工具](https://www.reddit.com/r/gamedev/comments/1s9kcl2/built_an_ai_tool_that_mines_steam_competitor/)（已核实存在此贴；工具本身质量未验证，且 Reddit 需翻墙）。

**可直接照做的免费步骤**（不用任何付费工具）：

1. 选 5–8 款竞品：Steam 搜 light gun / rail shooter 标签（如 *The House of the Dead Remake*、*Time Crisis* 替代品、*Operation Wolf Returns*、VR 射击里偏街机的）。
2. 用 Steam 官方公开 API 拉评论（无需 Key，无需翻墙程度：Steam 社区域名在国内访问不稳定，必要时用加速器/镜像；估计）：
   `https://store.steampowered.com/appreviews/<appid>?json=1&language=all&num_per_page=100&filter=recent`
3. 每款取"最有价值"差评 50 条 + 好评 50 条，存 CSV。
4. 喂给 Kimi/DeepSeek（长上下文），提示词模板：
   ```
   以下是游戏《X》的100条Steam评测。请：
   1) 把每条编码到主题（命名主题+父类别），输出主题-提及次数表；
   2) 只保留提及≥5次的主题；
   3) 对每个主题各引用2条原文；
   4) 最后输出：玩家最想要的3个改进、最常见的3个爽点、属于品类通病 vs 该游戏独有的区分。
   ```
5. 横向合并 5 款的主题表，得到品类级需求清单。

### 4.2 抖音同类内容评论区挖掘

工具（国内直连，均已核实存在；价格为估计，以官网为准）：

| 工具 | 功能 | 价格 | 链接 |
|---|---|---|---|
| 蝉妈妈 | 直播弹幕/视频评论聚合、"评价词谱"关键词分析、竞品直播间画像 | 有免费版，付费版数百–数千元/年（估计）| [chanmama.com](https://www.chanmama.com/)（[评论分析实操文章](https://www.sohu.com/a/1010314039_122634602)）|
| 飞瓜数据 | 竞品账号/商品分析、评论热词 | 付费为主（估计）| feigua.cn |
| 巨量算数（抖音官方）| 关键词热度、人群画像 | **免费** | trendinsight.oceanengine.com |
| 抖音搜索+人工 | 搜"光枪""射击游戏 电视""手机当鼠标 游戏"看评论区 | 免费 | — |

免费工作流（与 4.1 同构）：

1. 抖音搜"光枪游戏""射击游戏直播""电视游戏枪"等，找 10–20 条高赞视频/直播间录屏。
2. 每条手动复制或截图评论区 Top 50 评论（约 500–1000 条总量），存文本。
3. 同一套 LLM 主题编码提示词跑一遍；重点关注：观众在问"这是什么游戏/设备"（=需求信号）、"多少钱/哪里买"（=购买意向）、"看着晕/打不准/延迟"（=痛点）。
4. 用巨量算数验证关键词趋势，避免被单条视频的评论区样本带偏。
5. 阈值纪律（沿用 Flares 方法）：**提及次数 <5 的需求不做进设计**。

---

## 五、链接清单（按用途分类）

**光枪/关卡设计规律**
- BFI《Time Crisis at 30》: https://www.bfi.org.uk/features/time-crisis-30-how-peekaboo-with-guns-put-cabinet-gaming-back-target
- IGN Point Blank 评测: https://www.ign.com/articles/1998/04/13/point-blank-4
- GameSpot Point Blank 3: https://www.gamespot.com/articles/point-blank-3-hands-on/1100-2683243/
- GamePlaza Point Blank: https://gameplaza.ch/exhibition/arcade-cabinet/point-blank/?lang=en
- House of the Dead 系列（Wikipedia）: https://en.wikipedia.org/wiki/The_House_of_the_Dead
- 动态难度（GameDeveloper）: https://www.gamedeveloper.com/design/more-than-meets-the-eye-the-secrets-of-dynamic-difficulty-adjustment
- The Level Design Book - Pacing: https://book.leveldesignbook.com/process/preproduction/pacing
- GameDesign.gg - Pacing: https://gamedesign.gg/articles/pacing-in-level-design/

**AI 策划工作流（中文、免翻墙）**
- 腾讯游戏学堂《AI 策划团队从 0 到 1（CodeBuddy+PromptX）》: http://gameinstitute.qq.com/news/detail/321
- GameLook《腾讯云林哲：基于 AI Agent 的小游戏开发实践》: http://www.gamelook.com.cn/2026/06/595411/
- Summer Engine《2026 游戏设计 AI 工具指南》: https://www.summerengine.com/zh/blog/ai-tools-for-game-design
- 国产模型 API 价格对比（第三方汇总，估计值）: https://bumo.cc/blog/llm-api-price-comparison-2025-deepseek-qwen-doubao-kimi

**会话/核心循环设计**
- GameAnalytics 核心循环: https://www.gameanalytics.com/blog/how-to-perfect-your-games-core-loop
- Game-Ace 街机设计: https://game-ace.com/blog/arcade-game-design/

**竞品评论分析**
- Flares 竞品评论方法论: https://www.flares.tech/sources/competitor-reviews
- AWS 生成式 AI 分析游戏评论: https://aws.amazon.com/blogs/gametech/using-generative-ai-to-analyze-game-reviews-from-players-and-press/
- LLM-Cure 论文: https://arxiv.org/pdf/2409.15724
- 蝉妈妈直播评论分析实操: https://www.sohu.com/a/1010314039_122634602
- Steam 评论 API（非官方文档，用法见 4.1 步骤）

**访问性总注**：标注需翻墙的来源——Reddit、YouTube、BFI、IGN、GameSpot、GameDeveloper、arXiv（不稳定）。国内替代已在各节内注明（B 站/知乎/蝉妈妈/巨量算数/国产模型官网）。两个最关键的 AI 策划案例（腾讯系）均为中文且可直连。

# R5：AI Agent 编程工作流调研 —— 一人用 AI 写整个游戏

> 调研日期：2026 年（基于当时公开资料）。标注说明：**【已核实】**= 有公开页面/官方文档/一手复盘佐证；**【估计】**= 推测或价格随时变动，行动前请再核对。
>
> 目标场景：中国独立开发者一人 + AI 全工种，做 3-5 分钟一局的 3D 光枪射击游戏，PC 先行、后续 Android 电视，输入层为 UDP 坐标流 + HTTP 事件。

---

## TL;DR（先看这个）

1. **2026 年模型能力已经"够用"，决定成败的是工作流的两个边**：AI 编辑时能不能看到你的真实工程（上下文边）、写完代码后闭环验证有多快（反馈边）。优先把这两件事做掉，而不是纠结选哪个模型。
2. **Godot 是目前对 AI 最友好的主流引擎**：.tscn/.tres/.gd 全是文本文件，AI 可直接读写和做版本管理；Unity/Unreal 必须依赖 MCP 插件桥接编辑器。
3. **CLAUDE.md / AGENTS.md 是杠杆最大的单点投入**：控制在 200 行以内、写具体可执行的规则（含"禁止使用"清单）、踩坑后及时回写。
4. **频繁 git commit 是 AI 编程的生命线**：每小时至少一次、每个功能一次，AI 改崩了好回退。开工第一天就建仓库。
5. **测试策略要分层**：把游戏数值/状态机/积分规则抽成"无引擎纯逻辑层"让 AI 写单元测试（可行且收益高）；引擎层用 GdUnit4 场景测试 + headless 冒烟；手感、画面、"好不好玩"AI 测不了，必须人玩。
6. **"先找到 30 秒的好玩"是原型期最高准则**（Halo 设计师 Jaime Griesemer 的经典原则），一个核心循环不好玩，堆再多内容也没用。
7. **已有人用 AI 完整做出并上架游戏且赚到钱**：fly.pieter.com（浏览器飞行游戏，自称 17 天做到 $1M ARR）、《无为修仙传》（Steam 上架 10 天净收约 1.38 万元人民币）。共同点：人都没"躺平"，每天高强度验收、迭代、和玩家互动。
8. **国内直连可用的工具**：Trae 国内版、通义灵码、Kimi Code、智谱 GLM Coding Plan 等；Claude Code / Cursor / ChatGPT Codex 需翻墙 + 海外支付方式。

---

## ① 各 AI 编程工具开发游戏的实战经验

### 工具总览表

| 工具 | 价格（估计，以官网为准） | 中国大陆直连 | 授权/形态 | 游戏开发适配度（已核实的社区反馈） |
|---|---|---|---|---|
| **Claude Code**（Anthropic） | Pro $20/月起，Max $100~200/月（reddit 有开发者用 $200 Max 档做 Godot） | ❌ 需翻墙 + 海外支付 | 订阅制，终端 Agent | GDScript/Godot 多步任务链表现好，Godot 3/4 语法漂移少；但纯 Agent 模式下游戏开发能力"明显弱于全栈 Web 项目"（reddit 一手反馈） |
| **Cursor** | Pro 约 $20/月，有免费额度 | ⚠️ 需翻墙，网络不稳 | 订阅制，VS Code 系 IDE | 上下文索引强；Pieter Levels 用它 3 小时做出 fly.pieter.com 原型 |
| **Codex（OpenAI）** | 含在 ChatGPT Plus $20/月 / Pro $200/月 | ❌ 需翻墙 + 海外支付 | 订阅制，CLI + 云任务 | 公开的游戏专项复盘较少，通用工程能力强 |
| **Windsurf**（已被 Cognition 收购） | Pro 约 $15/月 | ⚠️ 需翻墙 | 订阅制，IDE | 与 Cursor 同档；游戏专项经验贴少 |
| **Trae 国内版**（字节跳动） | 国内版免费（估计：长期有免费额度，Pro 另行收费）；国际版 Pro 约 $10/月 | ✅ 直连 | 免费 + 订阅，IDE/插件 | Builder 模式可自然语言生成完整项目；国产工具横评中"一次性跑通率"领先（媒体评测口径，估计） |
| **通义灵码**（阿里） | 个人版免费 | ✅ 直连 | 免费，IDE 插件 | 代码续写和中文理解强；Agent 自主开发能力弱于 Claude Code（横评共识，估计） |
| **Kimi Code CLI**（月之暗面） | 有免费额度，订阅制 | ✅ 直连 | 订阅制，终端 Agent | 你正在用的工具；终端 Agent 形态与 Claude Code 类似 |
| **智谱 GLM Coding Plan** | 约 ¥数十元/月起（估计） | ✅ 直连 | 订阅制，可接入 Claude Code 兼容客户端 | 常被用作 Claude Code 的国内平替后端 |

来源：[Cursor vs Windsurf vs Claude Code 对比](https://docs.bswen.com/blog/2026-03-25-ai-coding-agents-comparison/)、[2026 主流 AI 编程工具横评（掘金）](https://juejin.cn/post/7654020257112637478)、[国产 AI 编程工具四强评测](https://post.smzdm.com/p/aww3qdv2/)、[国产工具横评 2026](https://nav.useaiwriter.com/articles/chinese-ai-coding-tools-comparison-2026)

### Claude Code + Godot：最成熟的一条路径【已核实】

中文社区最完整的一篇实操指南是 [《Godot + Claude Code 游戏开发完全指南》（GitHub Gist，2026-03 更新）](https://gist.github.com/xbfool/af1cacc74b7e58364aa244770bf85752)，核心要点：

**三层工具栈（可共存）：**

| 层 | 工具 | 作用 | 安装 |
|---|---|---|---|
| MCP Server | [GDAI MCP](https://github.com/3ddelano/gdai-mcp-plugin-godot)（免费开源） | 让 AI 实时控制 Godot 编辑器：建场景/节点/脚本、读报错 | Godot Asset Library 搜 "GDAI MCP" 装插件 → 底部标签页复制 JSON → `claude mcp add gdai-mcp -- uv run <复制的路径>` |
| Skills（知识） | [Godot-Claude-Skills](https://github.com/Randroids-Dojo/Godot-Claude-Skills)（免费） | GDScript 模式、GdUnit4 测试、导出、CI/CD | 克隆后把 `.claude/skills/` 复制进项目 |
| Skills（生成） | [godogen](https://github.com/htdt/godogen)（免费） | 从游戏描述生成完整项目 + 截图自检做视觉 QA | `./publish.sh ~/my-game` 初始化 |

**编辑器设置关键两步**（否则 AI 改的代码编辑器不刷新）：
`Editor → Editor Settings → 开启 Auto Reload Scripts on External Change` 和 `Auto Reload and Parse Scripts on Save`。

**踩坑经验（原文总结，已核实为多人共识）：**
- ⚠️ LLM 的 GDScript 训练数据少 → 用 godogen 内置的 850+ Godot API 离线文档，或在 CLAUDE.md 里写死 Godot 4 语法规则
- ⚠️ 复杂 .tscn 不要让 AI 手写（ext_resource/sub_resource 引用容易断）→ 复杂场景走 GDAI MCP 让编辑器 API 操作
- ⚠️ 频繁 commit（详见 ②）
- ⚠️ 有人全程让 AI 写，最后完全看不懂自己的代码 → 每次生成后花时间读懂，至少理解架构
- Godot 最大优势：UI 操作几乎都落在文本文件里，AI 不需 GUI 即可读写

另有 [claude-code-game-development](https://github.com/HermeticOrmus/claude-code-game-development)（22 个游戏插件 + 80 章手册，免费）和 [headless-godot-skill-kit](https://github.com/abagames/headless-godot-skill-kit)（不打开编辑器、纯 CLI 开发 Godot 的 skill 包，免费）可备选。实战长文：[用 Claude 写全部代码做 Godot RTS（dev.to 系列）](https://dev.to/datadeer/part-1-building-an-rts-in-godot-what-if-claude-writes-all-code-49f9)，以及[用 git worktree 并行跑多个 Claude 会话](https://dev.to/datadeer/part-2-running-multiple-claude-code-sessions-in-parallel-with-git-worktree-165i)。

### Claude Code + Unity【已核实】

- [Mythmatic：《8 Months of Unity Dev with Claude Code》](https://www.youtube.com/watch?v=GxZLC00yJ5g)（YouTube，11.7 万播放，[B 站中配版](https://www.bilibili.com/video/BV1bFk7BvEuL/)）：8 个月 Unity + Claude Code 的真实复盘。展示的工作流：让 Claude 先生成实现计划（implementation plan）→ 用自定义 "audit" 流程跑多个 sub-agent 交叉审查 → 再实施。**有效用法：先规划后实施、多 agent 审计；痛点：Unity 场景/预制体是二进制向 YAML，AI 直接改容易坏，需要 MCP 桥接。**
- [博客园：Claude Code + MCP 从空文件夹到能玩的 Unity 游戏](https://www.cnblogs.com/shanyou/p/23181231)：全程没手写一行代码，用语音指令 + 大白话 prompt 驱动，物理/材质/动画/粒子都跑通——证明"零手写"可行，但这是 demo 级。
- Reddit 一手反馈（[$200/月计划做 Godot 2D 自走棋](https://www.reddit.com/r/gamedev/comments/1mt5oii/is_claude_code_worth_it_for_godot_my_experience/)）：**即使在 Plan 模式下，Agent 做游戏的能力明显不如全栈 Web 项目**——原因是游戏的 bug 大多是运行时 bug（手感、时序、物理），不是静态检查能发现的。

### 方法论层面最重要的一篇【已核实】

[Summer Engine：《The Godot AI Workflow in 2026: What Agentic Coding Actually Means》](https://www.summerengine.com/blog/godot-agentic-coding)（注意：此文有推广自家产品的立场，但分析框架很扎实）：

> 所有 AI 游戏工作流都是同一个四段循环：**意图 → 编辑 → 运行 → 反馈**。拉开差距的是两条边：
> - **上下文边**：AI 编辑时能不能读到你的真实场景树、节点名、信号。修法（由弱到强）：手动粘贴 → 接 Godot MCP server（免费开源，"这是你能做的最大单项质量提升"）→ 用 AI 原生引擎。
> - **反馈边**：写完代码后谁按运行键、谁读报错。大多数 Godot bug 是运行时 bug（物理穿插、信号时序、第三波才崩的空引用），"文件写完了"≠"功能能玩"。不能运行游戏的 AI 只是"带额外步骤的自动补全"。

选型建议（照此对号入座）：
- 想留在原编辑器、只要代码更好 → Cursor/Claude Code + Godot MCP server，运行验证仍归你
- 想要长任务链 → Claude Code（终端）+ MCP
- 想让 AI 自己跑游戏、读截图、自纠错 → 需要 [Godot Runtime Bridge](https://www.linkedin.com/posts/matt-munroe-93688396_github-aesthetic-enginegodot-runtime-bridge-activity-7457853559330611200-5NQm) 这类运行时桥（AI 能启动游戏、点 UI、读场景树、截图再改代码）或 godogen 的截图自检 skill

### 国产工具实战经验

- [字节 Trae](https://juejin.cn/post/7654020257112637478)：Builder/SOLO 模式可从自然语言描述生成完整项目，国内免费直连，适合快速搭原型；深度工程能力（大型多文件重构）弱于 Claude Code（横评共识，**估计**）。
- [通义灵码](https://developer.aliyun.com/article/1666660)：定位是 IDE 插件而非自主 Agent，擅长补全和阿里云生态；一个人做游戏时它更像"副驾驶"而不是"外包工"。
- **务实建议（估计）**：国内网络环境下，主 Agent 用 Kimi Code / Trae（直连稳定），若追求最强模型再考虑翻墙用 Claude Code 或给兼容客户端接 GLM Coding Plan 作后端。游戏项目对模型的"长任务链 + 工具调用"能力要求高，免费档很容易撞到额度墙，预算上留出每月 ¥150~1500 的弹性。

---

## ② 工程纪律：版本管理 / 任务拆分 / 防跑偏 / 上下文管理

### git 纪律【已核实，多来源共识】

- **开工第一天就 `git init`**，不要等到"代码有价值了"再管。
- **至少每小时 commit 一次，每完成一个功能 commit 一次**（Godot+Claude 指南原文；JavaGuide 的 vibe coding 技巧帖同样把"先把 Git 准备好"列为第一条）。
- 用 Conventional Commits 格式（`feat: / fix: / refactor:`），AI 生成的提交信息要人审一眼。
- 进阶：用 `git worktree` 同时跑多个 Agent 会话（主线做 gameplay、分支做 UI），完成后 merge。参考 [dev.to 并行实践](https://dev.to/datadeer/part-2-running-multiple-claude-code-sessions-in-parallel-with-git-worktree-165i)。
- Godot 项目的 .gitignore 必加：`.godot/`、`export/`、`*.import` 可提交但 `.godot/imported/` 不提交。

### 任务拆分粒度【已核实 + 经验法则】

- **一次任务 = 一个可独立验证的功能点**（"给玩家加冲刺"可以；"做完整个战斗系统"不行）。Mythmatic 的做法：先让 AI 出实现计划文档，人确认后拆成可执行步骤再实施。
- 每个任务结束必须有**验证动作**：跑测试 / headless 加载场景 / 截图 / 人玩 30 秒。没有验证动作的任务不算完成。
- 开工前"把范围写窄"：一句话能说清、不含"并且/顺便"（参考 [GamineAI 的砍 scope 准则](https://gamineai.com/blog/how-to-create-a-video-game-with-chatgpt-5-5-step-by-step-beginner-guide-2026)：三行写不下核心玩法就砍）。

### CLAUDE.md / AGENTS.md 写法【已核实，多来源一致】

综合 [Better Stack《12 Rules for an Effective claude.md》](https://betterstack.com/community/guides/ai/claude-md-rules/)、[CLAUDE.md Best Practices（Medium）](https://medium.com/@onix_react/claude-md-best-practices-a60d726dc2a5)、[Hasan Arief 的 AGENTS.md 笔记](https://notes.hasanarief.dev/guides/agents-md-best-practices/)、[Build This Now](https://www.buildthisnow.com/blog/tools/claude-md-best-practices)：

1. **200 行以内**。Anthropic 官方建议每个 CLAUDE.md 控制在 200 行下——太长会稀释重要规则，agent 会"安静地不再遵守"。反直觉的维护规则：**agent 违反某条规则时，修法通常是删掉其他规则，而不是把这条加粗放大。**
2. **项目概述 3-5 句话**，给 AI 一个锐利的心智模型，不要写品牌故事。
3. **写规则，不写愿望**。"写干净的代码"是废话（无法执行）；"用 `@export` 不用旧版 `export var`，信号命名用过去时"是可执行规则。每条规则都要具体到 agent 能自动照做。
4. **"禁止使用"清单和"允许使用"清单同样重要**。明确排除（例：不要手写复杂 .tscn、不要碰 addons/ 目录、不要用 Godot 3 语法）。
5. **分层**：项目根 CLAUDE.md 放稳定事实（技术栈、目录约定、测试命令）；易变的坑放 memory/notes 文件；只有频繁复用的流程才做成 skill。别把所有东西塞进一个文件。
6. **动态更新**：项目加了功能、踩了新坑，当天回写 CLAUDE.md。它是一活文档。

可直接套用的 Godot 版模板（来自 [Gist 指南](https://gist.github.com/xbfool/af1cacc74b7e58364aa244770bf85752)，已核实可用）要点：引擎版本写死 / 严格类型标注（`var speed: float = 100.0`）/ Godot 4 语法（`@export`、`@onready`、`await`，禁 `yield`）/ 信号驱动架构（节点间不直接引用）/ 命名约定（节点 PascalCase、变量 snake_case、信号过去时、常量 UPPER_SNAKE）/ 数据进 .tres 不硬编码 / 每功能一 commit、每小时至少一 commit。

### 上下文管理【已核实】

- **一个会话只做一件事**：上下文污染是跑偏主因。做完一个功能就开新会话，让 CLAUDE.md 负责传递长期记忆，而不是靠聊天记录。
- 大改造前先让 agent 输出计划文档存进仓库（如 `docs/plans/xxx.md`），后续会话直接引用该文件，比口头重述可靠。
- 上下文将满时主动收尾：让 agent 把当前进度、未决问题写进文件，再开新会话接续（即"把记忆外置到磁盘"）。
- 多 agent 审计（Mythmatic 模式）：实现 agent 写完后，用独立 sub-agent 以"审查者"身份读代码找问题——实现者自己的上下文里带着偏见，审查者没有。

---

## ③ 一人项目的测试策略

### 可行性判断【已核实 + 估计】

**AI 写单元测试是 AI 编程中性价比最高的用法之一**，但前提是代码可测。游戏代码要分层：

| 层 | 例子（对应你的光枪游戏） | 测试方式 | AI 写测试的可行性 |
|---|---|---|---|
| 纯逻辑层（无引擎依赖） | 积分/连击规则、关卡波次配置解析、难度曲线、命中率统计、UDP 包解析、坐标滤波/校准算法 | 普通单元测试，几毫秒跑完 | ★★★★★ 完全可行，让 AI 先写测试再写实现 |
| 引擎交互层 | 命中判定（射线检测）、敌人状态机、场景切换 | GdUnit4 场景测试 / Unity PlayMode 测试 | ★★★ 可行但慢，AI 容易写出脆测试，需人审 |
| 表现/手感层 | 后坐力手感、特效、画面"一眼惊艳" | 自动化冒烟（能启动、不崩、截图非全黑）+ 人玩 | ★ AI 只能做冒烟，"好不好玩"必须人判断 |

**关键工程决策（强烈建议）**：第一天就把游戏设计成"纯逻辑核心 + 薄引擎壳"。例如瞄准坐标处理流水线（UDP 收包 → 去抖/滤波 → 屏幕坐标映射）写成不依赖引擎的模块，配单元测试；敌人波次生成器输入是时间和随机种子，输出是数据，天然可测。这也是 Summer Engine 文中"反馈边"的落地：纯逻辑层的反馈边用测试毫秒级闭合，引擎层才需要跑游戏。

### 具体工具

- **Godot**：[GdUnit4](https://github.com/godot-gdunit-labs/gdUnit4)（免费，MIT）——嵌入式单元测试框架，支持 GDScript/C#/场景测试、mocking、输入模拟；[官方文档](https://godot-gdunit-labs.github.io/gdUnit4/latest/)。替代：[GUT](https://github.com/bitwes/Gut)（免费）。
- **Unity**：Unity Test Framework（内置免费），[EditMode / PlayMode 测试](https://docs.unity3d.com/6000.2/Documentation/Manual/test-framework/course/play-mode-tests.html)分属不同程序集。
- **自动化冒烟**：
  - Godot 支持 headless 运行：`godot --headless --quit-after 300` 可验证"游戏能启动跑 5 秒不崩"；[godot-headless-verify skill](https://www.skillsdirectory.com/skills/infometa-godot-headless-verify) 专门做"这个场景还能不能实例化"的快速加载测试。
  - 导出构建冒烟：GitHub Actions 有现成的 [godot-ci](https://github.com/marketplace/actions/godot-ci)、[Build Godot](https://github.com/marketplace/actions/build-godot) Action；[Android 导出自动化教程](https://thedigitalspell.com/godot-github-actions-1/)（对你后续上 Android 电视直接有用）。
  - 视觉冒烟：godogen 的截图自检 skill 让 AI 运行游戏→截图→自己看画面是否正常（黑屏/模型丢失能抓出来）。
- **给 AI 的测试纪律**（写进 CLAUDE.md）：`每个核心系统至少一个测试；改 bug 必须先写一个能复现该 bug 的失败测试；提交前必须跑 tests/ 全绿`。

---

## ④ 原型快速迭代的社区方法论

### "先做 30 秒好玩"【已核实，有出处】

出自 Halo 1/2 设计师 **Jaime Griesemer** 的经典论述（[claritypotion 的引述与解读](https://claritypotion.com/2024/04/15/30-second-fun-focusing-principle/)）：

> "Halo 1 里其实只有大约 30 秒的好玩，一遍又一遍地重复。如果你能做出 30 秒的好玩，你就能做出 60 秒的、5 分钟的、几小时的。"

推论（社区通行的执行版）：
1. **第一周唯一目标：做出好玩的 30 秒核心循环**。对你的光枪游戏就是"瞄准→开火→命中反馈"这一循环——命中瞬间的音效、屏幕震动、敌人碎裂特效，比任何系统都优先。
2. 核心循环没验证好玩之前，不做 meta 系统（升级、解锁、商店）。
3. 用"30 秒测试"自检设计：ACTION（玩家做动作）→ FEEDBACK（游戏响应）→ REWARD（玩家感到爽），三段缺一段就补哪段（参考 [codefather 的 game-design skill](https://ai.codefather.cn/skills/2014279415528632321) 对 30-Second Test 的整理）。

### Game jam 式砍需求纪律【已核实为社区共识】

- **一句话法则**：核心玩法三行写不下、需要说"并且还能…"，就砍 scope（[GamineAI](https://gamineai.com/blog/how-to-create-a-video-game-with-chatgpt-5-5-step-by-step-beginner-guide-2026)）。
- **prototype → vertical slice**：原型验证"好不好玩"用灰盒（方块+胶囊）；确认好玩后再做垂直切片（thin but complete：完整走通一局 3-5 分钟的所有环节，但只有一关精修）。垂直切片还要留下产能测算数据——每关内容量 × 制作耗时 = 总工期（[Kioto Gaming 的对比指南](https://kiotogaming.com/prototype-vs-vertical-slice/)）。
- **每天保持一个能玩的 build**：game jam 的第一教训是"死前随时有可展示版本"。配合 ② 的 git 纪律，每晚打一个可运行包。
- AI 时代的特殊提醒（来自《无为修仙传》复盘，已核实）：**AI 让"做出有模有样的 demo"变得极容易，但"做出完成度高的商业游戏"绝对不轻松**——demo 到成品之间是每天 2-3 个版本的迭代、玩家反馈、自测、打包，人负责验收和方向，AI 负责实现，两边缺一不可。

### 对你项目的迭代路线建议（估计，供参考）

1. 第 1-3 天：灰盒核心循环——方块敌人 + 准星 + 命中反馈，手机 UDP 输入打通，先做到"30 秒好玩"
2. 第 1-2 周：垂直切片——完整一局 3 分钟（开场→三波敌人→结算→投币重来），一关精修到"直播间一眼惊艳"
3. 之后：按垂直切片的产能数据决定总内容量；每天可玩 build + 每晚 commit

---

## ⑤ AI 完整做出并上架游戏的真实案例

### 案例 1：fly.pieter.com —— Pieter Levels【已核实，数字为作者自报】

- **做了什么**：浏览器多人 3D 飞行模拟游戏，约 90~100% 代码由 AI 生成，工具为 **Cursor + Grok 3 + Claude 3.7 Sonnet + Three.js**。
- **耗时**：2025-02-22 开建，**初始原型约 3 小时**（他无游戏开发经验，第一句话 prompt 是"make a 3D flying game in the browser"）。
- **收入**：作者自称 17 天达到约 $1M ARR（约 $87K MRR，主要靠游戏内广告牌/内购），获 Elon Musk、Patrick Collison 转发助推。第三方无法独立审计当前收入，[stealwhatworks 的核查](https://stealwhatworks.com/blogs/news/vibecoded-apps-over-10k)也只确认到"曾经达到 $87K MRR"。
- **复盘要点**：病毒传播（名人转发）是收入爆发的直接原因，不可复制性高；但"3 小时出可玩原型"这一点被无数人复现验证过，可信。
- 来源：[codingwithvibe](https://codingwithvibe.com/vibe-coded-apps/)、[aiwiki 的 vibe coding 词条](https://aiwiki.ai/wiki/vibe_coding)、[BuiltWithAgents](https://www.builtwithagents.ai/strategy/vibe-coded-flight-sim-1m-arr-17-days)、[digital4africa](https://digital4africa.com/how-to-host-a-vibe-coded-website/)

### 案例 2：《无为修仙传》—— 程序员小灰（中国开发者，最贴近你的情况）【已核实】

- **做了什么**：仙侠放置类 Steam 游戏，AI 编程生成，是作者用 AI 做的**第三款**游戏（前两款销量惨淡）。
- **数据**：上线 10 天净销 **1564 份**，净收入 **$2064（约 ¥13845）**，最高同时在线 296 人，Steam 独立游戏约前 15%。
- **方法**：上线后建玩家 QQ 群，**平均每天迭代 2-3 个版本**；"人负责测试、验收和把关方向，AI 负责生成代码和修改实现"。
- **流量结论（对你做抖音直播有直接参考价值）**：前两款有公众号推广照样惨淡；这款发布前靠 Steam 自然流量攒了 300+ 愿望单，正式发售后自媒体转化最多只占 20%——**产品力本身才是自然流量的入场券，粉丝盘子只是加分项**。
- **策略修正**：原本计划"AI 一口气做 10 款"，出小爆款后改为"跑通一个再放大，先把它迭代精致"。
- 来源：[完整复盘（机核系转载）](https://www.jxxy.net/ai/articles/xiaohui-ai-game-steam-10day-review/)，原作者 X：@XiaohuiAI666

### 案例 3：Mythmatic（Daniel）【已核实】

- 8 个月用 Claude Code 开发 Unity 3D 游戏的持续 devlog（[YouTube](https://www.youtube.com/watch?v=GxZLC00yJ5g)，11.7 万播放；[B 站中配](https://www.bilibili.com/video/BV1bFk7BvEuL/)）。价值不在收入数字，而在**长期项目的工作流**：计划先行、sub-agent 审计、自定义规则文件。证明 AI 辅助做 3D 游戏可以撑过 8 个月不崩，但前提是人有 3D 美术功底（他 2008 年起做 3D）来补齐 AI 的短板。

### 案例 4：Reddit 上的 8 个月 vibe-coded Steam 游戏【已核实存在，细节有限】

- [《I vibe-coded a full Steam game in 8 months》](https://www.reddit.com/r/vibecoding/comments/1u9g7ym/i_vibecoded_a_full_steam_game_in_8_months_demo/)：demo 已上线。评论区共识："2025 年后几乎所有游戏都在某种程度上用了 AI"，且"公开宣传 made with AI 反而会伤害游戏口碑"——**对你的启示：营销时不要主打"AI 制作"，主打玩法和直播效果**。

### 案例共同点（结论）

1. 原型极快（小时级），成品极慢（月级）——时间花在人做验收、迭代、内容上。
2. 赚到钱的人都有发行/流量动作（愿望单、社群、直播），不是"做完就有人买"。
3. 人的不可替代环节：玩自己的游戏、判断好不好玩、和玩家互动、定方向。
4. 第三款才跑出来是常态——前两款当学费，别押注第一款。

---

## ⑥ 针对本项目（3D 光枪射击 + 手机当枪）的落地清单

1. **引擎选 Godot 4**（免费 MIT、文本化资源对 AI 最友好、导出 PC/Android 都免费）；除非你已有 Unity 积累。
2. **第一天做的事**：`git init` → 写 CLAUDE.md（用 ② 的模板）→ 装 GDAI MCP（或等价 Godot MCP）→ 把"瞄准输入流水线"设计为无引擎纯逻辑模块。
3. **手机输入层（UDP x,y 流 + HTTP 事件）天然适合 AI + 测试**：协议解析、坐标滤波、断线重连全是纯逻辑，让 AI 先写测试再写实现；做一个"鼠标模拟手机输入"的调试模式，人不依赖真机也能开发。
4. **先 30 秒**：命中反馈三件套（音效+震屏+粒子）先于一切系统。
5. **测试三层**：纯逻辑单元测试（AI 写）→ GdUnit4 场景测试（AI 写人审）→ headless 冒烟 + 截图自检（进 CI）→ 人玩（每天）。
6. **纪律**：每功能一 commit、每小时至少一 commit；上下文快满就把进度写进 `docs/` 开新会话；每周回顾一次 CLAUDE.md 删旧补新。
7. **营销纪律**：不宣传"AI 制作"，宣传玩法与直播效果；发售前攒愿望单/观众群。

---

## 链接清单

**工具/插件（全部免费开源除非注明）**
- GDAI MCP（Godot 编辑器 ↔ AI 桥）：https://github.com/3ddelano/gdai-mcp-plugin-godot ，文档 https://gdaimcp.com/docs/installation
- Godot-Claude-Skills：https://github.com/Randroids-Dojo/Godot-Claude-Skills
- godogen（项目生成 + 截图自检）：https://github.com/htdt/godogen
- Claude-GDSkill：https://github.com/Kothulhu94/Claude-GDSkill
- claude-code-game-development（22 游戏插件 + 手册）：https://github.com/HermeticOrmus/claude-code-game-development
- headless-godot-skill-kit：https://github.com/abagames/headless-godot-skill-kit
- GdUnit4 测试框架：https://github.com/godot-gdunit-labs/gdUnit4 ，文档 https://godot-gdunit-labs.github.io/gdUnit4/latest/
- godot-ci / Build Godot（CI Action）：https://github.com/marketplace/actions/godot-ci ，https://github.com/marketplace/actions/build-godot
- godot-headless-verify skill：https://www.skillsdirectory.com/skills/infometa-godot-headless-verify

**实战经验/复盘**
- Godot + Claude Code 完全指南（中文）：https://gist.github.com/xbfool/af1cacc74b7e58364aa244770bf85752
- Claude Code 做 Godot RTS（全代码由 Claude 写）：https://dev.to/datadeer/part-1-building-an-rts-in-godot-what-if-claude-writes-all-code-49f9
- git worktree 并行多会话：https://dev.to/datadeer/part-2-running-multiple-claude-code-sessions-in-parallel-with-git-worktree-165i
- Claude Code 值不值得做 Godot（reddit 一手）：https://www.reddit.com/r/gamedev/comments/1mt5oii/is_claude_code_worth_it_for_godot_my_experience/
- Claude Code + MCP 从空文件夹做出 Unity 游戏（中文）：https://www.cnblogs.com/shanyou/p/23181231
- Mythmatic 8 个月 Unity + Claude Code：https://www.youtube.com/watch?v=GxZLC00yJ5g ，B 站中配 https://www.bilibili.com/video/BV1bFk7BvEuL/
- Godot + AI 独立开发工作流（B 站）：https://www.bilibili.com/video/BV1xBrDB9E42/
- Summer Engine：Godot Agentic 工作流分析：https://www.summerengine.com/blog/godot-agentic-coding ，Claude for Godot 配置：https://www.summerengine.com/zh/blog/claude-for-godot
- Godot Runtime Bridge（AI 自己跑游戏）：https://www.linkedin.com/posts/matt-munroe-93688396_github-aesthetic-enginegodot-runtime-bridge-activity-7457853559330611200-5NQm
- Claude Code 游戏开发设置与 prompts：https://gamedevaihub.com/claude-game-development-guide/

**工程纪律 / CLAUDE.md**
- 12 Rules for an Effective claude.md：https://betterstack.com/community/guides/ai/claude-md-rules/
- CLAUDE.md Best Practices：https://medium.com/@onix_react/claude-md-best-practices-a60d726dc2a5
- AGENTS.md/CLAUDE.md 最佳实践（200 行规则）：https://notes.hasanarief.dev/guides/agents-md-best-practices/
- CLAUDE.md 四个作用域：https://www.buildthisnow.com/blog/tools/claude-md-best-practices
- Vibe Coding 实用技巧（Git/Spec/上下文，中文）：https://javaguide.cn/ai-coding/practices/the-cool-tricks-for-vibe-coding.html
- Claude Code 官方最佳实践：https://code.claude.com/docs/en/best-practices

**工具横评与国产替代**
- Cursor vs Windsurf vs Claude Code：https://docs.bswen.com/blog/2026-03-25-ai-coding-agents-comparison/
- 2026 主流 AI 编程工具横评（掘金，中文）：https://juejin.cn/post/7654020257112637478
- 国产四强评测（Trae/CodeBuddy/通义灵码/文心快码）：https://post.smzdm.com/p/aww3qdv2/
- 国产工具横评 2026：https://nav.useaiwriter.com/articles/chinese-ai-coding-tools-comparison-2026
- 通义灵码深度评测（阿里云）：https://developer.aliyun.com/article/1666660

**原型方法论 / 测试**
- "30 seconds of fun" 原则出处与解读：https://claritypotion.com/2024/04/15/30-second-fun-focusing-principle/
- 30-Second Test / 核心循环设计：https://ai.codefather.cn/skills/2014279415528632321
- Prototype vs Vertical Slice：https://kiotogaming.com/prototype-vs-vertical-slice/
- 砍 scope 准则：https://gamineai.com/blog/how-to-create-a-video-game-with-chatgpt-5-5-step-by-step-beginner-guide-2026
- Unity PlayMode 测试官方文档：https://docs.unity3d.com/6000.2/Documentation/Manual/test-framework/course/play-mode-tests.html
- Godot Android 导出 CI 教程：https://thedigitalspell.com/godot-github-actions-1/

**真实案例**
- fly.pieter.com 案例汇总：https://codingwithvibe.com/vibe-coded-apps/ ，收入核查 https://stealwhatworks.com/blogs/news/vibecoded-apps-over-10k ，https://www.builtwithagents.ai/strategy/vibe-coded-flight-sim-1m-arr-17-days
- 《无为修仙传》10 天复盘（中文）：https://www.jxxy.net/ai/articles/xiaohui-ai-game-steam-10day-review/
- 8 个月 vibe-coded Steam 游戏（reddit）：https://www.reddit.com/r/vibecoding/comments/1u9g7ym/i_vibecoded_a_full_steam_game_in_8_months_demo/

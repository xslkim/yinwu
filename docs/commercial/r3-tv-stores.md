# R3：电视应用商店上架自研游戏——流程、资质与分成调研

> 调研日期：2026 年（以最新可检索资料为准）
> 调研目的：评估"在电视应用商店上架自研光枪/体感游戏"作为商业化路径的可行性
> 标注约定：【已核实】= 有官方文档或权威来源直接证实；【推测】= 基于间接信息推断；【风险】= 对用户现有商业化想法的重大制约

---

## 一、国内电视应用商店

### 1.1 小米电视应用商店

【已核实】来源：[小米澎湃OS开发者平台《电视应用简介与上架》](https://dev.mi.com/xiaomihyperos/documentation/detail?pId=1356)

- **个人开发者直接被排除**：官方原文"小米电视应用商店暂不接受个人开发者账号申请新增电视应用"，必须注册企业开发者账号。用户的一人公司（OPC）有营业执照即可满足，但纯个人身份走不通。
- **双备案是硬门槛**（2023 年 9 月起）：
  - **工信部 APP 备案号**：开发者自行申请，上架审核时核验，且需在应用内展示备案号；
  - **广电总局备案号**：含音视频内容的视听类应用需提供七大牌照方（银河、未来电视、百视通、华数、南方新媒体、快乐阳光、国广东方）之一的盖章授权函，并由牌照方在"互联网电视应用白名单管理系统"中报备；**非视听类应用**（游戏通常属此类）由小米代为提交至其合作牌照方备案，需邮件联系商务。
- **上架前置流程**：正式提交前必须先邮件联系小米电视应用商店商务（文档直接给出商务邮箱），附公司介绍、应用介绍、资质、产品体验包。即电视商店不是"注册-上传-上架"的自助流程，而是商务驱动。
- **审核周期**：新增应用约 **14–30 个工作日**（含商务资质审核、运营审核、监管方审核、测试审核）；更新约 7–14 个工作日。
- **名称要求**：应用名必须与工信部备案名、软著名一致。

分成比例：小米官方 TV 文档未公开写明。历史上小米电视/平板游戏联运曾为开发者 70%（2015 年，[雷峰网](https://www.leiphone.com/category/zixun/xCKPRyPmX3Hf2fZN.html)、[搜狐](https://www.sohu.com/a/28077621_204728)），但 TV 端当前联运分成【未核实，需商务洽谈】，国内安卓渠道游戏联运惯例为 50/50。

### 1.2 海信（聚好看）

【已核实】来源：[聚好看开发者生态安全政策](https://dev.hismarttv.com/doc/16)

资质硬要求（缺一不可）：

1. 有效 **ICP 证明**，且备案主体与开发者**企业信息**一致（隐含要求企业主体）；
2. 《计算机软件著作权证书》（或 APP 电子版权认证证书），且须与发布应用、企业信息一致；
3. 必须完成**工信部 APP 备案**，应用内显著位置展示备案号并支持跳转核验。

对游戏类应用的专项要求：

- 严格落实**实名认证 + 防沉迷**，执行未成年人时段时长限制（第 3.4.1 条第 14 款）；
- 强化"氪金"管控（第 15 款）；
- 游戏不得为破解版/汉化版（知识产权条款）。

【风险】对用户方案特别重要的一条：**"禁止出现跨境通信的情况，既不允许与中国之外的 IP 产生通信行为"**（3.2.8 条）。如果游戏服务器部署在海外、或计划国内外共用一套后端，在海信渠道会直接违规。

### 1.3 当贝市场（ZNDS 开发者平台）

【已核实】来源：[ZNDS 开发者平台](http://dev.znds.com/home.php)

- 第三方电视商店中最开放的一个：官网明示"开放应用提交，无需繁杂程序，即可将您的应用添加到当贝市场中"，流程为：注册账号 → 提交应用 → 应用审核 → 应用上架 → 后续合作。
- 提供**当贝支付 SDK**（电视端内购计费）和广告 SDK，游戏变现需要接入其支付体系。
- 有"开发者扶持计划"（QQ 群对接）。
- 分成比例：【未核实】官网未公开数字，联运/支付分成需商务洽谈。电视渠道历史惯例为 5:5 左右（推测）。

【推测】当贝作为非终端厂商的第三方商店，个人/小团队上架门槛通常低于小米、海信，但同样会要求软著和 APP 备案（工信部备案是全国性强制要求，任何分发平台都要核验）。

### 1.4 华为智慧屏（AppGallery TV）

【已核实】来源：[华为开发者问答](https://developer.huawei.com/consumer/cn/forum/topic/0203148746492524481)、[华为应用资质审核要求](https://developer.huawei.com/consumer/cn/doc/development/app/80301)、[InfoQ：无内购无广告单机游戏是否需要版号](https://www.infoq.cn/article/ewqPbgKb3iqDeu9V2r5J)

- 智慧屏应用通过 AppGallery Connect 上架（与手机应用同一体系），个人开发者实名认证后可以创建和发布应用（[华为官方问答](https://developer.huawei.com/consumer/cn/forum/topic/0202217974329121254)）。
- **【重大风险】游戏版号是硬门槛**：华为应用市场明确要求**无论单机还是网游、无论是否含内购/广告/联网，均需提供《网络游戏出版物号（ISBN）核发单》或版号批文**，未提交版号无法通过审核（官方"答开发者问"多次确认）。另有软著要求。
- 华为应用市场国内分成：应用内购买华为抽成情况参见[电子发烧友报道](https://www.elecfans.com/d/1175101.html)（华为曾宣布部分品类调整至 1:9，游戏联运国内惯例 5:5，TV 端未公开）。

### 1.5 TCL（欢视商店）/ 其他厂商商店

【未找到公开的自助开发者门户条款】TCL 国内电视应用商店（欢视）未见公开上架文档，行业惯例是商务对接制。【推测】资质要求与小米/海信对齐（企业主体 + 软著 + 工信部备案 + 广电备案）。

### 1.6 电视游戏版号政策现状（重点）

【已核实】来源：[上海圣剑网络（新三板）年报风险提示](https://www.neeq.com.cn/disclosure/2022/2022-04-25/1650875882_206729.pdf)（2021 年报，后续年份年报延续同一表述）

- 移动游戏必须取得版号方可运营（《关于移动游戏出版服务管理的通知》）；
- 但**基于机顶盒/电视运行的电视游戏，目前主管部门认为尚无法律规定其上线运营须取得审查批准文号，且未发布申请程序的操作指引，无行政受理通道——即想办也办不了**。这是新三板公司向上海市新闻出版局法务科访谈后的正式披露，可信度较高。
- 由此形成**灰色地带**：政策上没有受理通道（电视游戏办不了版号），但华为等商店的审核规则一刀切要求版号 → **自研电视游戏在华为商店实际上无法上架**；小米/海信/当贝走的是"工信部备案 + 广电备案"路线，不强制版号，但必须落实实名认证和防沉迷。
- 【推测】这种"商店规则严于监管现状"的局面，意味着电视游戏上架的实际可行性取决于各商店审核口径，且政策随时可能收紧（参考 181 号文体系下的白名单制度，[广电总局广科院](http://www.abs.ac.cn/art/2023/4/11/art_1236_40955.html)）。

---

## 二、海外电视应用商店

### 2.1 Google Play（Android TV / Google TV）

【已核实】

- **费用**：开发者账号一次性 **$25**（[TMS 提交清单](https://www.tms-outsource.com/cs/app-store-google-play-submission-checklist/)）。
- **分成**：订阅与内购——年收入前 100 万美元 **15%**，超出部分 30%（[SplitMetrics](https://splitmetrics.com/blog/google-play-apple-app-store-fees/)、[Slashdot](https://tech.slashdot.org/story/21/03/16/1612230/google-play-drops-commissions-to-15-from-30-following-apples-move-last-year)）。对小开发者（年收 < $1M）实际就是 15%。
- TV 应用与普通应用共用 Play Console，需声明支持 Android TV（leanback），TV 应用 target API 要求略低于手机（API 34 vs 35，[clearlaunch.dev](https://clearlaunch.dev/checklists/google-play-submission)）。
- **【坑】个人账号限制**：2023 年 11 月 13 日后注册的**个人开发者账号**，必须先完成"至少 12 名测试者连续 14 天 opted-in 的封闭测试"才能申请正式发布（[testerscommunity](https://www.testerscommunity.com/guides/how-many-testers-do-you-need-google-play)）。组织账号（需邓白氏编码 D-U-N-S）无此要求。
- 身份验证：Google 正全面推行 Android 开发者验证（个人需政府证件+住址证明，[Doply](https://www.doply.io/en/blog/android-developer-verification-explained)）。

### 2.2 Amazon Fire TV（Amazon Appstore）

【已核实】来源：[Amazon 开发者社区](https://community.amazondeveloper.com/t/how-much-does-it-cost-to-publish-apps-on-the-amazon-appstore/2187)、[AFTVnews](https://www.aftvnews.com/amazon-reduces-its-cut-of-appstore-revenue-from-30-to-20-effective-last-week-leaving-developers-with-more-revenue/)、[TechCrunch](https://techcrunch.com/2021/06/17/amazons-appstore-lowers-its-cut-of-developer-revenue-for-small-businesses-adds-aws-credits/)

- **费用**：注册开发者账号和提交应用**完全免费**（无年费、无上架费）。
- **分成**：标准 30%（开发者 70%）；年收入 < 100 万美元的开发者自动纳入 Small Business Accelerator，**抽成降至 20%**（开发者 80%），另送相当于收入 10% 的 AWS 抵扣额度（2021 年 12 月起自动生效）。
- 对小型独立开发者而言，Amazon 是海外电视平台中**门槛最低**的（免费 + 80% 分成 + Fire TV 在北美客厅设备市占高）。

### 2.3 Samsung Tizen（Samsung TV Seller Office）

【已核实】来源：[Samsung TV Seller Office 条款](https://seller.samsungapps.com/tv/portal/terms?seq=9)

- **分成**：条款原文"shall be shared by Samsung and you at the rate of 30 per cent for Samsung and 70 per cent for you"——**三星抽 30%，开发者 70%**（付费应用/内购）。
- 上架经 Seller Office 提交，需 TV 端认证测试（性能、内存、UI 合规，[BitCot](https://www.bitcot.com/smart-tv-app-development/)），注册本身免费。
- 技术栈：Web 应用（HTML5/JS）打包为 .wgt，需要 Tizen 签名证书。
- 注意：Samsung Galaxy Store（手机/平板）2025 年起改为订阅 85/15（[Samsung Developer](https://developer.samsung.com/sdp/news/en/2025/03/13/new-revenue-share-model-for-galaxy-store)），但 TV Seller Office 条款仍是 70/30。

### 2.4 LG webOS（LG Content Store）

【已核实】

- 开发者账号**免费**（[webostv.developer.lge.com](https://webostv.developer.lge.com/login)，[Reddit 实操帖](https://www.reddit.com/r/LGOLED/comments/wzs6hg/adfree_youtube_webos_app/)）。
- 支持应用内购买（[LG 官方 IAP 文档](https://webostv.developer.lge.com/develop/guides/in-app-purchase)）。
- 分成比例：【未核实】LG 官方未公开统一的 revenue share 数字，行业默认对齐 70/30（推测）。
- 技术栈同为 Web 应用（.ipk）；Developer Mode 允许侧载调试，会话 1000 小时过期。

### 2.5 海外平台对比小结

| 平台 | 上架费 | 分成（开发者） | 个人可上架 | 备注 |
|---|---|---|---|---|
| Google Play TV | $25 一次性 | 85%（<$1M/年） | 可（有 12 测试者/14 天门槛） | 国内身份验证需准备证件 |
| Amazon Fire TV | 免费 | 80%（<$1M/年） | 可 | 门槛最低 |
| Samsung Tizen TV | 免费 | 70% | 可 | 需通过 TV 认证测试 |
| LG webOS | 免费 | 未公开（推测 70%） | 可 | 分成条款不透明 |

---

## 三、电视游戏付费模式与国内市场规模

### 3.1 付费模式现状

- **国内电视端**：买断制几乎不存在；内购通过电视端扫码支付（微信/支付宝扫码）完成，由渠道支付 SDK（如当贝支付）代收并抽成；大量"免费+广告"应用。广电"套娃收费"治理（2023 年起）对电视端收费项目监管趋严（[慧聪广电网行业动态](http://broadcast.hczyw.com/t-hu-lian-wang-dian-shi.html)）。
- **海外 TV 端**：以免费+广告（AVOD）、订阅为主流，游戏类内购存在但盘子小；Netflix Games 等案例说明 TV/大屏游戏付费模式仍在摸索（[IT之家](https://www.ithome.com/0/743/657.htm)）。
- 【推测】对用户的"卖硬件送游戏"模式而言，电视游戏本身更适合做**免费预装/免费下载的硬件增值内容**，而不是直接卖游戏——这与健身环大冒险的逻辑一致，调研数据支持这个方向：直接靠电视游戏内购/买断赚钱的市场证据非常弱。

### 3.2 国内电视游戏市场真实规模

【已核实】

- 2015 年（智能电视游戏概念最热时）中国电视游戏实际销售收入仅 **2.2 亿元**（当年游戏产业总收入 1407 亿，占比约 0.16%）（[52dtv 论坛转载游戏工委报告](http://bbs.52dtv.com/thread-31890-1-1.html)）。
- 2024 年中国游戏市场总收入 3257.83 亿元（[国家新闻出版署](https://www.nppa.gov.cn/xxfb/ywdt/202412/t20241216_877445.html)），官方报告细分中**已不再单列"电视游戏"**；与之最接近的主机游戏 2024 年收入 44.88 亿元（占比约 1.4%，靠《黑神话》拉动，[IT之家](https://www.ithome.com/0/817/443.htm)）。
- OTT 大屏激活量约 2.6 亿台、日开机近 1 亿台（2020 年数据，[36氪](https://www.36kr.com/p/1051294309482243)）——**终端保有量巨大，但游戏付费生态极小**，大屏收入主要靠视频会员和广告。

【结论】国内"电视游戏市场"作为独立品类在统计意义上接近于零；硬件保有量 ≠ 游戏市场。靠电视游戏直接收费盈利没有市场基础，硬件+免费内容模式是更合理的路径。

---

## 四、个人开发者上架的门槛与坑（汇总）

1. **企业主体几乎是国内电视商店的前提**：小米明示不接受个人开发者；海信要求 ICP 主体与企业一致。用户已有一人公司（OPC），可用营业执照解决，但需同步完成：对公账户、ICP 备案、APP 备案、软著（每款游戏约需 1–3 个月下证）。
2. **版号悖论**：电视游戏在监管上无版号受理通道（办不了），但华为一刀切要求版号（上不去）；其他商店不强制版号但强制实名认证+防沉迷接入（需要接版署实名系统或渠道 SDK）。
3. **商务驱动而非自助上架**：小米要求上架前先发邮件建联商务，审核 14–30 个工作日——节奏远慢于手机商店，不适合快速迭代试错。
4. **海信禁止跨境通信**：国内外共用服务器架构会在海信渠道违规；国内渠道需要国内服务器 + ICP 备案。
5. **Google Play 个人账号的 12 测试者 × 14 天封闭测试**是 2023 年后的新坑，发布计划要预留至少 3 周。
6. **国内电视游戏没有直接付费市场**（见 3.2）：上架电视商店的合理定位是"硬件用户的免费内容分发渠道"，而非收入来源；谈联运分成意义不大。
7. **海外是更现实的游戏分发路径**：Amazon Fire TV（免费+80% 分成）、Google Play TV（$25+85%）对个人友好，但 TV 游戏同样以免费+广告为主流，盈利重心仍应放在硬件上。
8. 【推测】若走"硬件配套 App"路线，可考虑**绕开电视商店**：游戏随 AR 枪以 APK 形式直接安装/扫码下载（当贝等第三方商店对非视听应用相对宽松，且侧载在安卓电视上可行），把商店上架作为获客补充而非唯一渠道——但绕开商店会失去搜索流量，且广电白名单体系下存在政策不确定性。

---

## 五、来源链接列表

国内商店与政策：
- [小米澎湃OS开发者平台：电视应用简介与上架](https://dev.mi.com/xiaomihyperos/documentation/detail?pId=1356)
- [聚好看开发者生态安全政策（海信）](https://dev.hismarttv.com/doc/16)
- [ZNDS 开发者平台（当贝）](http://dev.znds.com/home.php)
- [华为应用资质审核要求](https://developer.huawei.com/consumer/cn/doc/development/app/80301)
- [InfoQ：无内购无广告单机游戏上架是否需要版号（华为官方答复）](https://www.infoq.cn/article/ewqPbgKb3iqDeu9V2r5J)
- [上海圣剑网络 2021 年报（电视游戏版号灰色地带）](https://www.neeq.com.cn/disclosure/2022/2022-04-25/1650875882_206729.pdf)
- [广电总局广科院：互联网电视应用白名单系统](http://www.abs.ac.cn/art/2023/4/11/art_1236_40955.html)
- [甘肃广电：互联网电视 ID 管理与白名单平台发布](http://app.gdj.gansu.gov.cn/home/news/detail/aid/15333.html)
- [雷峰网：小米平板电视游戏开发者分成提至 70%（2015）](https://www.leiphone.com/category/zixun/xCKPRyPmX3Hf2fZN.html)

海外商店：
- [Amazon 开发者社区：上架费用问答](https://community.amazondeveloper.com/t/how-much-does-it-cost-to-publish-apps-on-the-amazon-appstore/2187)
- [AFTVnews：Amazon Appstore 抽成降至 20%](https://www.aftvnews.com/amazon-reduces-its-cut-of-appstore-revenue-from-30-to-20-effective-last-week-leaving-developers-with-more-revenue/)
- [TechCrunch：Amazon 小企业加速器](https://techcrunch.com/2021/06/17/amazons-appstore-lowers-its-cut-of-developer-revenue-for-small-businesses-adds-aws-credits/)
- [SplitMetrics：Google Play / App Store 费用 2025](https://splitmetrics.com/blog/google-play-apple-app-store-fees/)
- [Google Play 12 测试者规则解析](https://www.testerscommunity.com/guides/how-many-testers-do-you-need-google-play)
- [Samsung TV Seller Office 条款（70/30）](https://seller.samsungapps.com/tv/portal/terms?seq=9)
- [Samsung Galaxy Store 新分成模型（订阅 85/15，手机端）](https://developer.samsung.com/sdp/news/en/2025/03/13/new-revenue-share-model-for-galaxy-store)
- [LG webOS TV 开发者（免费账号/IAP）](https://webostv.developer.lge.com/develop/guides/in-app-purchase)

市场规模：
- [国家新闻出版署：2024 年中国游戏产业报告要点](https://www.nppa.gov.cn/xxfb/ywdt/202412/t20241216_877445.html)
- [IT之家：2024 中国主机游戏市场 44.88 亿元](https://www.ithome.com/0/817/443.htm)
- [52dtv：2015 年电视游戏收入 2.2 亿元](http://bbs.52dtv.com/thread-31890-1-1.html)
- [36氪：大屏云游戏与 OTT 激活量数据](https://www.36kr.com/p/1051294309482243)

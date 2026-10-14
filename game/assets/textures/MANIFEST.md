# 美术素材清单（MANIFEST）

生成链路：本机 WSL ComfyUI（http://127.0.0.1:8188），模型 Z-Image-Turbo fp8（`z-image-turbo-fp8-e4m3fn.safetensors` + `qwen_3_4b` CLIP + `ae.safetensors` VAE）。
统一采样参数：steps=9, cfg=1.0, sampler=euler, scheduler=simple, denoise=1.0, batch=1。

统一风格前缀（所有素材共用）：

```
flat cartoon vector style, bold shapes, vibrant saturated colors, clean edges, thick outline, mobile game asset, simple geometry, no realistic detail
```

统一负面 prompt：

```
photorealistic, realistic, 3d render, gradient background, shadow, watermark, text, signature, blurry, noise, dark colors, muted, complex background, frame, border
```

透明背景处理（模型不输出 alpha，统一后处理）：
- `white` 模式：纯白底生成 → 从边缘洪水填充去除近白色 → 边缘羽化 → LANCZOS 缩放到目标尺寸。
- `chroma` 模式：品红底生成 → 按角点采样的实际底色做色度键抠除（准星含白色，不能用白底）。
- 生成脚本：`D:\yinwu\temp\gen_assets.py`；原始未抠图备份在 `D:\yinwu\temp\assets_raw\`。

## 靶脸（512×512，透明）

### target_face_normal.png
- 完整 prompt：{风格前缀}, round circular badge icon of a red cartoon villain face, angry fierce expression, furrowed brows, gritted teeth grin, evil but cute, thick black outline, perfectly round face filling the frame, centered, isolated on solid pure white background
- seed=678250163，生成 512×512，白底抠除，透明 36.2%

### target_face_hostage.png
- 完整 prompt：{风格前缀}, round circular badge icon of a green cartoon face, scared crying expression, big teary eyes, trembling mouth, innocent hostage, thick black outline, perfectly round face filling the frame, centered, isolated on solid pure white background
- seed=232272576，生成 512×512，白底抠除，透明 43.9%

### target_face_gold.png
- 完整 prompt：{风格前缀}, round circular badge icon of a golden yellow cartoon face wearing black sunglasses, smug confident grin, cool attitude, thick black outline, perfectly round face filling the frame, centered, isolated on solid pure white background
- seed=612825714，生成 512×512，白底抠除，透明 35.0%

## 场景贴图（不透明）

### sky.png（2048×1024）
- 完整 prompt：{风格前缀}, wide panoramic cartoon sky, bright blue sky with puffy white clouds, distant green rounded cartoon hills on the horizon, sunny day, seamless horizontally tileable panorama, no ground foreground, no sun, no birds
- seed=1253109286，原尺寸输出
- 注：底部含卡通草地（可用于地平线以下）；左右边缘为纯色天空+丘陵，近似无缝但未经严格环接验证

### ground.png（512×512）
- 完整 prompt：{风格前缀}, seamless tileable texture of cartoon green grass lawn, top-down view, flat saturated green with subtle darker green tufts pattern, repeating pattern, no objects, no shadows, uniform lighting
- seed=1873196877，原尺寸输出
- 注：平铺接缝未做严格环接，大面积平铺可能有轻微重复感

### wall.png（512×512）
- 完整 prompt：{风格前缀}, seamless tileable texture of a carnival circus booth wall, vertical red and white stripes like a circus tent, flat colors, repeating pattern, no objects, no text, uniform lighting
- seed=1592496243；后处理：裁掉顶部不可平铺的波浪檐，按条纹周期裁切（x=134..376，两个完整红+白周期，y=140..512）后缩放至 512×512，左右边缘像素颜色一致（205,22,30 ≈ 206,23,29），可四方连续

## 特效精灵（透明）

### muzzle_flash.png（512×512）
- 完整 prompt：{风格前缀}, cartoon muzzle flash starburst explosion, bright yellow glowing core, orange and red pointed star rays radiating outward, symmetrical burst shape, centered, isolated on solid pure white background
- seed=675315779，白底抠除，透明 64.3%

### particle_hit.png（256×256）
- 完整 prompt：{风格前缀}, cartoon impact hit effect, four-pointed star spark burst with small orange and yellow debris fragments flying outward, comic style pow effect, centered, isolated on solid pure white background
- seed=1378398888，生成 512×512 → 缩放 256×256，白底抠除，透明 71.3%

### particle_star.png（256×256）
- 完整 prompt：{风格前缀}, single shiny golden five-pointed star, glossy flat cartoon star icon, centered, isolated on solid pure white background
- seed=1700140712，生成 512×512 → 缩放 256×256，白底抠除，透明 70.2%

## UI 图标（256×256，透明）

### ui_coin.png
- 完整 prompt：{风格前缀}, arcade gold coin icon, shiny golden coin with a star embossed in the middle, slightly tilted, thick brown outline, centered, isolated on solid pure white background
- seed=1044937931，512→256，透明 47.6%

### ui_ammo.png
- 完整 prompt：{风格前缀}, cartoon bullet icon, single big brass and copper bullet with red tip, vertical orientation, thick dark outline, centered, isolated on solid pure white background
- seed=1260236152，512→256，透明 69.7%

### ui_crosshair.png
- 完整 prompt：{风格前缀}, crosshair reticle icon, white double concentric ring with thick black outline, hollow empty center with a small white dot, four short tick marks, perfectly centered and symmetrical, isolated on solid flat magenta background, pure magenta #FF00FF background
- seed=972399669，512→256
- 注：模型实际输出的底色是深品红 (218,8,115)，按角点采样色做色度键抠除（tol=65, feather=140），透明 68.3%；白心黑边双环、中心镂空带白点

## 宣传图

### poster.png（1024×1024）
- 完整 prompt：{风格前缀}, arcade shooting gallery game poster, dark silhouette of a player holding a smartphone in one hand raised up aiming it like a gun at colorful cartoon carnival targets, red villain target faces popping up, dramatic vibrant red and blue contrasting colors, dynamic composition, large empty space at the top for title text, no text
- seed=528195242（第二版；第一版 seed=1359169525 因武器画成手枪而非手机被重做）
- 顶部大面积纯蓝天留白，可直接压标题

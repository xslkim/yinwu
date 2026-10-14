# 素材来源与授权（ATTRIBUTION）

本目录素材全部由 AI 生成管线产出，版权归项目方并按各平台条款使用。详细生成参数见各子目录 MANIFEST.md。

## 贴图（textures/，14 张 PNG）

- 本机 ComfyUI（WSL, http://127.0.0.1:8188）+ **Z-Image-Turbo fp8** 模型生成
  （`z-image-turbo-fp8-e4m3fn.safetensors` + qwen_3_4b CLIP + ae.safetensors VAE），
  steps=9 / cfg=1.0 / euler / simple。
- 透明素材经后处理抠图（白底洪水填充 / 品红色度键，脚本 `temp/gen_assets.py`，原件 `temp/assets_raw/`）。
- Z-Image-Turbo 为开源模型（Tongyi Lab，Apache-2.0），生成图可商用。

## 音频（audio/，29 个 WAV，统一 -16 LUFS）

- **音效**（sfx_*.wav，17 个）：Stable Audio 3 small-sfx —— Stability AI **Community License**，
  年收入 < $1M 可商用。
- **BGM**（bgm_menu/play/intense.wav）：Mureka 付费 API（mureka-7.6），task id：
  - bgm_menu: `166324227604482`（候选0）
  - bgm_play: `166324854587394`（候选0）
  - bgm_intense: `166325943009281`（候选0）
  按平台条款获得生成内容使用权；原始 flac 备份在 `temp/arcade-audio/bgm_raw/`。
- **语音播报**（voice_*.wav，5 个）：**Qwen3-TTS**-12Hz-1.7B-CustomVoice（Apache-2.0 可商用），
  speaker=Uncle_Fu 模型预置合成音色，非真人录音。

## 接入约定

游戏侧只读本目录：贴图经 `AssetLib.texture_or_null()`、音频经 `AudioManager.play()/voice()/play_bgm()`
加载，文件缺失自动回退程序化占位（纯色/合成音）。事件名 ↔ 文件名映射见 `game/README.md`。

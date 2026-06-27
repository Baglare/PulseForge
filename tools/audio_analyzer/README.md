# PulseForge Debug Audio Analyzer

Bu araç, PCM WAV dosyalarındaki belirgin click/transient zamanlarını bulup PulseForge Unity debug beatmap JSON formatında çıktı üretir.

## Ne yapar?

- WAV dosyasını Python standart kütüphanesiyle okur.
- Mono ve stereo PCM WAV dosyalarını destekler.
- 8-bit, 16-bit ve 32-bit PCM örnekleri için basit normalized amplitude hesabı yapar.
- Frame bazlı basit peak/onset detection uygular.
- Unity tarafındaki `DebugBeatMapJsonParser` ile uyumlu `schemaVersion: 1` JSON üretir.

## Ne yapmaz?

- Final müzik analiz sistemi değildir.
- MP3 veya diğer sıkıştırılmış formatları okumaz.
- `librosa`, `numpy`, `scipy`, `ffmpeg`, `pydub` veya harici paket kullanmaz.
- Tempo, beat grid, bar/measure veya müzikal yapı tahmini yapmaz.
- Unity tarafında runtime analiz veya UI sağlamaz.

## Nasıl çalıştırılır?

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM.json `
  --display-name "Analyzed Debug 120 BPM" `
  --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike
```

`--output` verilmezse JSON stdout'a yazılır.

## Unity'ye nasıl verilir?

1. Üretilen `.json` dosyasını Unity projesinde `Assets/` altında bir klasöre koy.
2. Unity import ettikten sonra dosya `TextAsset` olarak görünür.
3. Debug prototype objesindeki `Debug Beat Map Json` alanına bu TextAsset'i ata.
4. Play Mode'da `Start / Restart` ile beatmap JSON'dan okunur.

## Önemli ayarlar

- `--frame-ms`: Analiz frame süresi. Varsayılan `10`.
- `--threshold-ratio`: En yüksek amplitude'a göre peak eşiği. Varsayılan `0.35`.
- `--min-gap-seconds`: İki peak arasındaki minimum süre. Varsayılan `0.18`.
- `--max-events`: Üretilecek maksimum event sayısı.
- `--pattern`: Event action sırası. Sadece `Guard` ve `Strike` kabul edilir; pattern biterse baştan döner.
- `--global-offset-seconds`: JSON içindeki global offset alanı.

## İlk sürüm sınırlamaları

- En iyi sonucu click track veya çok belirgin transient içeren debug seslerde verir.
- Gürültülü müzikte yanlış peak seçebilir.
- Çok sık transientlerde `--min-gap-seconds` ayarı sonucu ciddi şekilde etkiler.
- Float WAV desteği hedeflenmemiştir.

## Test

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

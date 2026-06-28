# PulseForge

**PulseForge**, bir ses dosyasındaki ritmik vuruşları analiz edip bunları oynanabilir bir ritim-dövüş prototipine dönüştürmeyi amaçlayan deneysel bir Unity projesidir.

Şu anki sürüm final oyun değildir. Bu repo, önce sağlam bir ritim oynanış çekirdeği kurmayı, sonra ses analizinden gelen veriyi Unity içinde oynanabilir beatmap olarak kullanmayı hedefleyen aşamalı bir prototiptir. Yani büyülü “her şarkıyı kusursuz oyuna çeviren” sistem değil; ölçülebilir, test edilebilir ve geliştirilebilir bir temel. Daha az masal, daha çok mühendislik.

---

## Mevcut durum

Şu ana kadar iki ana milestone tamamlandı:

| Milestone | Durum | Açıklama |
|---|---:|---|
| Milestone 1: Debug Rhythm Combat Prototype | Tamamlandı | Unity içinde ritim judgement, lane, score, combo, combat feedback ve DSP audio clock ile çalışan debug prototip. |
| Milestone 2: Audio Pipeline Prototype | Tamamlandı | Python tabanlı WAV analyzer, diagnostics, postprocessor, comparison tool, pipeline runner ve Unity Editor pipeline window. |

Şu anda sistem şu akışı destekler:

```text
WAV audio
  ↓
Python audio analyzer
  ↓
Raw beatmap JSON
  ↓
Playable beatmap postprocessor
  ↓
Unity JSON import
  ↓
Debug rhythm-combat prototype
```

---

## Temel özellikler

### Unity runtime / debug prototype

- `RhythmSession` tabanlı ritim oturumu.
- `Perfect`, `Good`, `Miss` judgement sistemi.
- `Guard` ve `Strike` aksiyonları.
- `Pending`, `Hit`, `Missed` event yaşam döngüsü.
- Timeout ile kaçırılan eventlerin otomatik `Missed` olması.
- Score, combo, max combo ve sonuç sayaçları.
- OnGUI tabanlı debug rhythm lane.
- OnGUI tabanlı combat feedback paneli:
  - `PERFECT PARRY`
  - `GOOD PARRY`
  - `PERFECT SLASH`
  - `GOOD SLASH`
  - `MISS / HIT TAKEN`
- `RealtimeSongClock` ve `DspAudioSongClock` desteği.
- AudioClip atanırsa DSP tabanlı zamanlama.
- Debug beatmap için üç kaynak:
  1. JSON `TextAsset`
  2. `DebugBeatMapAsset` ScriptableObject
  3. Hardcoded fallback beatmap
- Start countdown.
- Aynı anda `Guard + Strike` spam davranışını engelleyen ambiguous input koruması.
- Debug timing calibration paneli:
  - Beatmap offset
  - Input offset
  - Son timing error gösterimi

### Python audio pipeline

`tools/audio_analyzer/` altında çalışan araçlar:

- `generate_debug_click_track.py`
  - Bilinen click zamanlarında deterministic WAV üretir.
- `pulseforge_audio_analyzer.py`
  - PCM WAV dosyasından ritmik peak/onset noktaları çıkarır.
  - `amplitude` ve `onset` detection mode destekler.
  - Unity uyumlu beatmap JSON üretir.
  - Analysis report JSON ve frame debug CSV üretebilir.
- `compare_beatmaps.py`
  - Expected ve actual beatmap JSON dosyalarını karşılaştırır.
  - Ortalama hata, maksimum hata ve önerilen global offset hesaplar.
- `postprocess_beatmap.py`
  - Raw analyzer output’unu daha oynanabilir beatmap’e dönüştürür.
  - `easy`, `normal`, `hard` difficulty presetleri.
  - `preserve`, `alternate`, `pattern`, `intensity` action mapping modları.
- `run_debug_pipeline.py`
  - WAV → raw JSON → playable JSON → optional compare report akışını tek komutla çalıştırır.

### Unity Editor tool

Unity menüsünden açılır:

```text
Tools > PulseForge > Audio Pipeline
```

Bu pencere üzerinden:

- WAV AudioClip seçilebilir.
- Detection mode seçilebilir.
- Difficulty seçilebilir.
- Action pattern verilebilir.
- Python pipeline çalıştırılabilir.
- Üretilen playable JSON Project panelinde seçilebilir.
- Seçili `DebugRhythmPrototypeController` objesine generated JSON atanabilir.

---

## Hızlı başlangıç

### 1. Unity demo sahnesini aç

Unity içinde şu sahneyi aç:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

Sahnede `Debug Rhythm Prototype` adlı GameObject bulunmalıdır. Bu objede `DebugRhythmPrototypeController` component’i vardır.

### 2. Demo audio ve beatmap kontrolü

Inspector’da şu alanlar atanmış olmalıdır:

```text
Debug Audio Clip:
PF_Debug_120BPM_DefaultBeatMap.wav

Debug Beat Map Json:
BM_Playable_Debug_120BPM.json
```

`Debug Beat Map Json` boşsa, proje `DebugBeatMapAsset` veya fallback hardcoded beatmap ile çalışabilir. Ancak pipeline-generated demo için JSON atanması tercih edilir.

### 3. Play Mode’da çalıştır

1. Play Mode’a gir.
2. `Start / Restart` butonuna bas.
3. Countdown bitince ses ve lane 0’dan başlar.
4. Kontroller:

```text
Space = Guard
J     = Strike
```

5. Lane üzerindeki markerlar hit line’a geldiğinde doğru inputu ver.
6. Score, combo, timing feedback ve combat feedback panelini izle.

---

## Python pipeline kullanımı

Python araçları standart kütüphane ile yazılmıştır. Harici paket gerekmez. Python komutları proje kökünden çalıştırılmalıdır.

### Testleri çalıştır

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

### Debug click track üret

```powershell
python tools/audio_analyzer/generate_debug_click_track.py --output Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav --times 1.00,1.50,2.00,2.50,3.00,3.25,3.75,4.25,4.75,5.25
```

### Raw beatmap üret

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav --output Assets/PulseForge/Demo/BeatMaps/BM_Raw_Debug_120BPM.json --display-name "Raw Debug 120 BPM" --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike --detection-mode amplitude --summary
```

### Playable beatmap üret

Debug click track içinde 3.00 ve 3.25 saniyelerde yakın eventler olduğu için demo map’te `hard` difficulty kullanmak daha uygundur. `normal` preset bu iki eventten birini seyreltebilir. Bu hata değil; postprocessor’ın oynanabilirlik filtresi çalışıyor, fazla itaatkâr küçük şey.

```powershell
python tools/audio_analyzer/postprocess_beatmap.py Assets/PulseForge/Demo/BeatMaps/BM_Raw_Debug_120BPM.json --output Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM.json --display-name "Playable Debug 120 BPM" --difficulty hard --action-mode pattern --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike --report-output tools/audio_analyzer/out/postprocess_debug_120bpm_report.json
```

### Expected beatmap ile karşılaştır

```powershell
python tools/audio_analyzer/compare_beatmaps.py Assets/PulseForge/Demo/BeatMaps/BM_Debug_120BPM_Default.json Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM.json --tolerance-ms 40 --report-output tools/audio_analyzer/out/compare_debug_120bpm.json
```

### Tek komutluk pipeline

```powershell
python tools/audio_analyzer/run_debug_pipeline.py --input-wav Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav --output-dir Assets/PulseForge/Demo/BeatMaps --name Debug_120BPM --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike --detection-mode amplitude --difficulty hard --action-mode pattern --expected-json Assets/PulseForge/Demo/BeatMaps/BM_Debug_120BPM_Default.json --summary
```

Bu komut şunları üretir:

```text
Assets/PulseForge/Demo/BeatMaps/BM_Raw_Debug_120BPM.json
Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM.json
tools/audio_analyzer/out/Debug_120BPM_analysis_report.json
tools/audio_analyzer/out/Debug_120BPM_postprocess_report.json
tools/audio_analyzer/out/Debug_120BPM_compare_report.json
```

`tools/audio_analyzer/out/` klasörü debug çıktıları içindir ve Git’e alınmamalıdır.

---

## Unity Editor Audio Pipeline Window

Unity içinde şu menüden açılır:

```text
Tools > PulseForge > Audio Pipeline
```

Önerilen demo ayarları:

```text
Input Audio Clip:
PF_Debug_120BPM_DefaultBeatMap.wav

Expected Beat Map Json:
BM_Debug_120BPM_Default.json

Output Name:
Debug_120BPM

Pattern:
Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike

Detection Mode:
amplitude

Difficulty:
hard

Action Mode:
pattern

Use Expected Compare:
true
```

`Run Pipeline` sonrası generated JSON bulunursa pencere bunu gösterebilir. İstenirse `Ping / Select Generated JSON` ile Project panelinde seçilebilir. Sahnede `DebugRhythmPrototypeController` olan GameObject seçiliyken `Assign to Selected Debug Prototype` butonu ile generated JSON component’e atanabilir.

Bu işlem sahneyi otomatik kaydetmez. Bilinçli tercih. Otomatik sahne kaydetmek, editor tool dünyasında küçük bir mayın tarlasıdır.

---

## Proje yapısı

```text
Assets/PulseForge/
├── Runtime/
│   ├── Domain/Rhythm/
│   │   ├── HitJudge.cs
│   │   ├── BeatEventRuntime.cs
│   │   ├── BeatEventMatcher.cs
│   │   ├── RhythmInputResolver.cs
│   │   ├── BeatEventTimeoutProcessor.cs
│   │   ├── RhythmSession.cs
│   │   └── ScoreTracker.cs
│   ├── Unity/
│   │   ├── BeatMaps/
│   │   │   ├── DebugBeatMapAsset.cs
│   │   │   └── DebugBeatMapJsonParser.cs
│   │   ├── Timing/
│   │   │   ├── ISongClock.cs
│   │   │   ├── RealtimeSongClock.cs
│   │   │   └── DspAudioSongClock.cs
│   │   └── Prototype/
│   │       ├── DebugRhythmPrototypeController.cs
│   │       ├── DebugRhythmLaneRenderer.cs
│   │       └── DebugCombatFeedbackRenderer.cs
├── Editor/
│   └── AudioPipeline/
│       └── PulseForgeAudioPipelineWindow.cs
└── Demo/
    ├── Audio/
    ├── BeatMaps/
    └── Scenes/

tools/audio_analyzer/
├── generate_debug_click_track.py
├── pulseforge_audio_analyzer.py
├── compare_beatmaps.py
├── postprocess_beatmap.py
├── run_debug_pipeline.py
├── tests/
└── README.md
```

---

## Mimari özet

PulseForge şu ayrımı korumaya çalışır:

```text
Domain layer
→ Unity bilmez.
→ Perfect / Good / Miss, event state, session ve score kurallarını taşır.

Unity runtime/debug layer
→ Domain sistemini sahne içinde çalıştırır.
→ Audio clock, lane, combat feedback ve debug HUD sağlar.

Unity editor layer
→ Python pipeline’ı Unity içinden çalıştırır.
→ Generated JSON’u bulur ve prototype’a atamayı kolaylaştırır.

Python tools
→ WAV analiz eder.
→ Raw beatmap üretir.
→ Playable beatmap’e postprocess eder.
→ Karşılaştırma ve diagnostics sağlar.
```

Bu ayrımın amacı basit: Ses analizi değişince Unity gameplay çekirdeği yıkılmasın. Unity UI değişince Python analyzer etkilenmesin. Her sınıf kendi haddini bilsin; yazılımda bu hâlâ devrim niteliğinde bir beklenti.

---

## Test stratejisi

### Unity Edit Mode testleri

Unity içinde:

```text
Window > General > Test Runner > EditMode > Run All
```

Test edilen ana davranışlar:

- Hit judgement sınırları.
- Beat event runtime state geçişleri.
- Pending event matching.
- Input resolve akışı.
- Timeout miss işleme.
- RhythmSession sayaçları.
- ScoreTracker davranışı.

### Python unittest

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

Test edilen ana davranışlar:

- WAV generator.
- Analyzer amplitude/onset mode.
- Diagnostics report ve CSV.
- Beatmap comparison.
- Playable postprocessor.
- Tek komutluk pipeline runner.

---

## Bilinçli sınırlamalar

Şu an sistemin yapmadıkları:

- MP3 runtime import yok.
- Gerçek zamanlı ses analizi yok.
- Librosa, numpy, scipy, ffmpeg gibi dış bağımlılıklar yok.
- Gerçek müziklerde kusursuz beat detection iddiası yok.
- Final UI yok.
- Gerçek animasyon/sprite combat sistemi yok.
- Tam beatmap editor yok.
- Seviye seçme veya kullanıcı profili yok.
- Online skor yok.

Bunlar eksiklik değil, bu milestone’un sınırları. Sınır koymak, projenin kendini batırmasını önler. İnsanlık bunu keşfedeli uzun zaman oldu ama uygulama kısmı hâlâ zayıf.

---

## Roadmap

Önerilen sıradaki adımlar:

1. Analyzer tuning
   - Gerçek müzik benzeri WAV dosyalarında onset mode ayarlarını test etmek.
   - Diagnostics CSV üzerinden threshold, baseline ve min-gap değerlerini iyileştirmek.

2. Beatmap visualization
   - Unity içinde raw/analyzed eventleri görsel olarak daha iyi inceleme.
   - Waveform veya energy curve debug görünümü.

3. Combat prototype upgrade
   - OnGUI yerine basit sprite/2D görsel sistem.
   - Parry/slash animasyonları.
   - Daha iyi feedback sesleri.

4. Beatmap authoring/editing
   - Generated beatmap üzerinde küçük düzeltmeler yapabilme.
   - Offset, action ve event silme/ekleme araçları.

5. Daha gelişmiş audio analysis
   - Dış bağımlılıklar değerlendirilirse librosa/essentia tabanlı ikinci analiz pipeline’ı.
   - BPM ve beat tracking.
   - Section/intensity segmentation.

---

## Portfolyo değeri

PulseForge şu açılardan portfolyoda güçlü durur:

- Unity gameplay runtime.
- Ritim judgement sistemi.
- Test edilmiş domain mimarisi.
- DSP audio clock kullanımı.
- JSON data pipeline.
- Python CLI tooling.
- Unity Editor tooling.
- Audio analysis başlangıcı.
- Data-driven beatmap workflow.
- Dokümante edilmiş milestone geliştirme süreci.

Bu proje sadece “Unity’de butona bastım” projesi değil. Runtime, tooling, veri akışı ve test tarafları birlikte düşünülmüş bir sistem. Bunu doğru anlatırsan teknik mülakatta malzeme çıkar. Yanlış anlatırsan yine “bir ritim oyunu yaptım” seviyesine düşer; o da fena değil ama daha düşük rütbe.

---

## İlgili dokümanlar

```text
docs/06-debug-prototype-milestone.md
docs/07-audio-pipeline-milestone.md
tools/audio_analyzer/README.md
```

---

## Kısa teknik özet

PulseForge şu an şunu kanıtlar:

```text
Ses dosyasından otomatik veya yarı otomatik ritim eventleri çıkarılabilir.
Bu eventler postprocess edilerek oynanabilir beatmap’e dönüştürülebilir.
Unity bu beatmap’i okuyup DSP audio clock ile senkron debug ritim-dövüş prototipinde oynatabilir.
```

Bu henüz final oyun değildir. Ama final oyuna doğru iyi kurulmuş bir temel ve gösterilebilir bir teknik prototiptir.

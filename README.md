# PulseForge

**PulseForge**, bir ses dosyasındaki ritmik vuruşları analiz edip bunları oynanabilir bir
ritim-dövüş prototipine dönüştürmeyi amaçlayan deneysel bir Unity projesidir.

Şu anki sürüm final oyun değildir. Bu repo, önce sağlam bir ritim oynanış çekirdeği
kurmayı, sonra ses analizinden gelen veriyi Unity içinde oynanabilir beatmap olarak
kullanmayı hedefleyen aşamalı bir prototiptir. Yani her şarkıyı kusursuz şekilde oyuna
çeviren final bir sistem değil; ölçülebilir, test edilebilir ve geliştirilebilir bir teknik
temeldir.

---

## Mevcut durum

Şu ana kadar beş ana milestone tamamlandı:

| Milestone | Durum | Açıklama |
|---|---:|---|
| Milestone 1: Debug Rhythm Combat Prototype | Tamamlandı | Unity içinde ritim judgement, lane, score, combo, combat feedback ve DSP audio clock ile çalışan debug prototip. |
| Milestone 2: Audio Pipeline Prototype | Tamamlandı | Python tabanlı WAV analyzer, diagnostics, postprocessor, comparison tool, pipeline runner ve Unity Editor pipeline window. |
| Milestone 3: Forge Preview / Beatmap Visualization | Tamamlandı | Unity Editor Audio Pipeline penceresinde raw/playable timeline preview, report summary paneli ve generated JSON atama akışı. |
| Milestone 4: Combat Visualization Prototype | Tamamlandı | OnGUI feedback'e ek olarak sahnede player/enemy, parry, slash, miss/hit taken ve intensity tabanlı efekt şiddeti gösteren debug combat visualization katmanı. |
| Milestone 5: Combat Style Variants | Tamamlandı | Aynı ritim analizinden Balanced, Defensive, Aggressive ve Bursty playable beatmap varyantları üretme, karşılaştırma, preview etme ve prototype'a atama akışı. |

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
Combat style variants
  ↓
Unity Editor Forge Preview / visualization
  ↓
Unity JSON import
  ↓
Debug rhythm-combat prototype
  ↓
OnGUI + scene combat feedback
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
- Sahne tabanlı `DebugCombatSceneView` feedback katmanı:
  - Player ve enemy için basit 2D primitive görseller.
  - Guard sonuçları için parry spark.
  - Strike sonuçları için diagonal slash.
  - Miss ve timeout için player hit taken flash/shake.
  - Perfect / Good ve `BeatEvent` intensity değerine göre görsel şiddet farkı.
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
  - `legacy`, `balanced`, `defensive`, `aggressive`, `bursty` combat-style presetleri.
- `run_debug_pipeline.py`
  - WAV → raw JSON → playable JSON → optional compare report akışını tek komutla çalıştırır.
  - `--combat-style` ile tek bir combat-style playable output üretebilir.
- `generate_style_variants.py`
  - Aynı WAV veya raw beatmap üzerinden Balanced / Defensive / Aggressive / Bursty playable JSON varyantları üretir.
  - Variant postprocess ve optional compare raporlarını diagnostics klasörüne yazabilir.

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
- Combat style seçilebilir.
- Python pipeline çalıştırılabilir.
- Style variant outputları üretilebilir.
- Raw ve playable beatmap JSON üretimi aynı pencerede görülebilir.
- Raw/playable eventler timeline üzerinde karşılaştırılabilir.
- Balanced / Defensive / Aggressive / Bursty variantları event count, Guard count, Strike count ve dropped count ile karşılaştırılabilir.
- Seçilen variant timeline preview hedefi yapılabilir.
- Analysis, postprocess ve compare raporları okunabilir özet olarak incelenebilir.
- Üretilen playable JSON Project panelinde seçilebilir.
- Seçili `DebugRhythmPrototypeController` objesine generated JSON veya style variant JSON atanabilir.

---

## Hızlı başlangıç

Demo videosu veya portfolyo kaydı için ayrıntılı akış: [docs/10-demo-recording-guide.md](docs/10-demo-recording-guide.md)

Demo sonrası repository temizliği için kontrol listesi: [docs/11-repository-cleanup-checklist.md](docs/11-repository-cleanup-checklist.md)

Combat Style Variants milestone ayrıntıları: [docs/12-combat-style-variants-milestone.md](docs/12-combat-style-variants-milestone.md)

Kısa demo akışı:

1. `Tools > PulseForge > Audio Pipeline` penceresini aç.
2. Demo WAV dosyasını seçip pipeline'ı çalıştır.
3. Timeline preview ve report summary alanlarını kontrol et.
4. Generated playable JSON'u seçili `DebugRhythmPrototypeController` objesine ata.
5. Demo sahnesini Play Mode'da çalıştır.

Kısa style variant test akışı:

1. `Tools > PulseForge > Audio Pipeline` penceresini aç.
2. WAV dosyasını seç.
3. `Generate Style Variants` çalıştır.
4. `Aggressive` veya `Defensive` variantını preview et.
5. Seçili `DebugRhythmPrototypeController` objesine variant JSON'u ata.
6. Play Mode'da Guard / Strike dağılımını ve sahne feedback'ini test et.

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

`Debug Beat Map Json` boşsa, proje `DebugBeatMapAsset` veya fallback hardcoded beatmap ile
çalışabilir. Ancak pipeline-generated demo için JSON atanması tercih edilir.

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

### 4. Sahne combat feedback'ini kontrol et

Milestone 4 ile OnGUI combat feedback'e ek olarak sahnede basit 2D combat feedback görülebilir.

1. Play Mode'da `Start / Restart` ile oturumu başlat.
2. `Space` ile Guard eventlerini yakala; sahnede player yakınında parry feedback görünmeli.
3. `J` ile Strike eventlerini yakala; enemy üzerinde slash feedback görünmeli.
4. Bilerek input kaçır veya event timeout olana kadar bekle; player tarafında `MISS / HIT TAKEN` feedback'i görünmeli.
5. Farklı intensity değerlerine sahip eventlerde efekt ölçeği, parlaklığı ve miss shake şiddetinin değiştiğini kontrol et.

Bu sahne feedback'i final art veya gerçek animasyon sistemi değildir. Harici sprite, texture,
material veya Animator Controller kullanmadan, ritim sonucunun sahnede okunabilir dövüş
feedback'ine dönüşmesini gösteren prototype katmanıdır.

### 5. Audio Pipeline penceresiyle Forge Preview akışı

1. Unity menüsünden `Tools > PulseForge > Audio Pipeline` penceresini aç.
2. `Input Audio Clip` alanına WAV tabanlı demo audio clip'i seç.
3. Gerekirse `Expected Beat Map JSON` alanına referans beatmap JSON'u ata.
4. Output name, pattern, detection mode, difficulty ve action mode değerlerini kontrol et.
5. `Run Pipeline` ile pipeline'ı çalıştır.
6. Timeline preview üzerinde raw ve playable event dağılımını kontrol et.
7. Reports panelinden analysis, postprocess ve compare özetlerini oku.
8. `Ping / Select Generated JSON` ile generated playable JSON'u Project panelinde seç.
9. Sahnede `DebugRhythmPrototypeController` bulunan GameObject'i seç.
10. `Assign to Selected Debug Prototype` ile generated playable JSON'u prototype'a ata.
11. Style variant testi için `Generate Style Variants` çalıştır.
12. `Style Variant Comparison` panelinde Balanced / Defensive / Aggressive / Bursty dağılımlarını kontrol et.
13. `Preview` ile istediğin variantı timeline preview'de göster.
14. `Assign` ile seçilen variant JSON'u prototype'a ata.

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

Debug click track içinde 3.00 ve 3.25 saniyelerde yakın eventler olduğu için demo map’te
`hard` difficulty kullanmak daha uygundur. `normal` preset bu iki eventten birini
seyreltebilir. Bu hata değil; postprocessor’ın oynanabilirlik filtresinin beklenen
davranışıdır.

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

### Combat-style varyantları üret

Milestone 5 ile aynı WAV veya raw beatmap üzerinden dört playable variant üretilebilir:

```powershell
python tools/audio_analyzer/generate_style_variants.py --input-wav Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav --output-dir Assets/PulseForge/Demo/BeatMaps --name Debug_120BPM --difficulty hard --detection-mode amplitude --summary
```

Bu akış varsayılan olarak şu playable JSON dosyalarını üretir:

```text
Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM_Balanced.json
Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM_Defensive.json
Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM_Aggressive.json
Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM_Bursty.json
```

`Defensive` Guard ağırlıklı, `Aggressive` Strike ağırlıklı dağılımı kontrol etmek için
kullanışlıdır. Ayrıntılar:
[docs/12-combat-style-variants-milestone.md](docs/12-combat-style-variants-milestone.md)

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

Combat Style:
legacy

Use Expected Compare:
true
```

`Run Pipeline` sonrası generated JSON bulunursa pencere bunu gösterebilir. `Combat Style`
değeri `legacy` iken eski `Action Mode` / `Pattern` workflow'u korunur. `balanced`,
`defensive`, `aggressive` veya `bursty` seçildiğinde action mapping combat-style preset
tarafından kontrol edilir.

Milestone 3 ile pencere raw/playable beatmap eventlerini timeline üzerinde gösterir ve
analysis, postprocess, compare raporlarını okunabilir özetlere dönüştürür. Böylece raw
analyzer output'u ile playable postprocessor output'u arasındaki fark Unity içinde
görülebilir.

Milestone 5 ile aynı pencerede ek olarak:

- `Generate Style Variants` ile Balanced / Defensive / Aggressive / Bursty playable JSON dosyaları üretilebilir.
- `Style Variant Comparison` panelinde event count, Guard count, Strike count, first/last time ve dropped count incelenebilir.
- Her variant `Ping / Select` ile Project panelinde bulunabilir.
- `Preview` ile timeline preview hedefi seçilen variant'a çevrilebilir.
- `Assign` ile seçili `DebugRhythmPrototypeController` objesine ilgili variant JSON atanabilir.

İstenirse `Ping / Select Generated JSON` ile generated playable JSON Project panelinde
seçilebilir. Sahnede `DebugRhythmPrototypeController` olan GameObject seçiliyken
`Assign to Selected Debug Prototype` butonu ile generated JSON component’e atanabilir.

Bu pencere final beatmap editor veya waveform editor değildir. Şu anki rolü pipeline çıktısını
incelemek, raporları özetlemek ve generated playable JSON'u prototype'a bağlamayı
kolaylaştırmaktır.

Bu işlem sahneyi otomatik kaydetmez. Bu tercih, editor aracının kullanıcı onayı olmadan sahne dosyasını değiştirmesini önlemek için korunur.

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
│   │       ├── DebugCombatFeedbackRenderer.cs
│   │       └── DebugCombatSceneView.cs
├── Editor/
│   └── AudioPipeline/
│       ├── BeatmapTimelinePreviewDrawer.cs
│       ├── PipelineReportSummaryDrawer.cs
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
├── generate_style_variants.py
├── tests/
│   ├── test_compare_beatmaps.py
│   ├── test_generate_style_variants.py
│   ├── test_postprocess_beatmap.py
│   ├── test_pulseforge_audio_analyzer.py
│   └── test_run_debug_pipeline.py
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
→ Audio clock, lane, OnGUI feedback, sahne combat feedback'i ve debug HUD sağlar.

Unity editor layer
→ Python pipeline’ı Unity içinden çalıştırır.
→ Generated JSON’u bulur ve prototype’a atamayı kolaylaştırır.
→ Raw/playable timeline preview ve pipeline report summary sağlar.
→ Style variant generation, comparison, preview ve assign akışını taşır.

Python tools
→ WAV analiz eder.
→ Raw beatmap üretir.
→ Playable beatmap’e postprocess eder.
→ Combat-style playable variantları üretir.
→ Karşılaştırma ve diagnostics sağlar.
```

Bu ayrımın amacı basit: Ses analizi değişince Unity gameplay çekirdeği yıkılmasın. Unity UI
veya sahne feedback'i değişince Python analyzer etkilenmesin. Her katmanın sorumluluğu
sınırlı kaldığında değişikliklerin etkisi daha kolay izlenir.

---

## Test stratejisi

Demo veya commit öncesinde en az Python unittest komutunu ve Unity Edit Mode test akışını
hatırla. Unity testleri bu ortamda çalıştırılamadıysa bunu sonuç raporunda açıkça belirt.

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
- Combat-style presetleri ve style variant generation.

---

## Bilinçli sınırlamalar

Şu an sistemin yapmadıkları:

- MP3 runtime import yok.
- Gerçek zamanlı ses analizi yok.
- Librosa, numpy, scipy, ffmpeg gibi dış bağımlılıklar yok.
- Gerçek müziklerde kusursuz beat detection iddiası yok.
- Final UI yok.
- Sahne combat feedback'i prototype seviyesindedir; final animasyon/sprite combat sistemi yok.
- Tam beatmap editor yok.
- Forge Preview final beatmap editor veya waveform editor değildir.
- Timeline preview şimdilik inceleme amaçlıdır; event authoring aracı değildir.
- Combat-style presetleri final koreografi üreticisi değildir; basit ve deterministik Guard / Strike dağılımlarıdır.
- JSON `schemaVersion: 1` hâlâ `Guard` / `Strike` aksiyonlarıyla sınırlıdır.
- Verse, chorus, drop gibi yapısal müzik analizi yoktur.
- HeavySlash, Burst veya benzeri ayrı combat event type'ları henüz yoktur.
- Seviye seçme veya kullanıcı profili yok.
- Online skor yok.

Bunlar eksiklik olarak değil, bu milestone’un bilinçli sınırları olarak değerlendirilmelidir.
Sınırların açık tutulması, prototipin teknik hedefini ve değerlendirme kapsamını netleştirir.

---

## Roadmap

Milestone 5 ile Combat Style Variants tamamlandı. Önerilen sıradaki hazırlık veya milestone başlıkları:

1. Project Cleanup / Demo Recording Prep
   - Demo sahnesi, README ve milestone dokümanlarını video kaydına hazır hale getirmek.
   - Kısa portfolyo akışını netleştirmek: pipeline → style variants → preview → assign → Play Mode → sahne combat feedback.
   - Gereksiz debug çıktılarını ve geçici dosyaları kontrol etmek.

2. Gerçek müzik WAV testleri
   - Debug click track dışındaki WAV dosyalarında analyzer ve combat-style presetlerini test etmek.
   - Defensive / Aggressive / Bursty çıktılarının gerçek müzikte ne kadar okunabilir olduğunu ölçmek.
   - Threshold, min-gap ve burst window değerlerini diagnostics üzerinden iyileştirmek.

3. Combat event type genişletmesi
   - `Guard` / `Strike` sınırının ötesinde metadata veya yeni action tipleri araştırmak.
   - HeavySlash, Burst veya GuardBreak gibi fikirleri JSON schema değişikliği gerektirmeden önce tasarlamak.
   - Runtime feedback tarafında hangi yeni actionların gerçekten değer kattığını test etmek.

Sonraki iyileştirme başlıkları:

4. Beatmap authoring/editing
   - Generated beatmap üzerinde küçük düzeltmeler yapabilme.
   - Offset, action ve event silme/ekleme araçları.

5. Analyzer tuning
   - Gerçek müzik benzeri WAV dosyalarında onset mode ayarlarını test etmek.
   - Diagnostics CSV üzerinden threshold, baseline ve min-gap değerlerini iyileştirmek.

6. Forge Preview polish
   - Timeline zoom veya ölçek kontrolü.
   - Daha ayrıntılı compare görselleştirmesi.
   - Waveform veya energy curve debug görünümü araştırması.

7. Daha gelişmiş audio analysis
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
- Forge Preview / Audio Pipeline Editor visualization.
- Combat Visualization Prototype.
- BeatEvent intensity değerinin sahne efekt şiddetine bağlanması.
- Combat Style Variants ile aynı inputtan farklı playable output üretimi.
- Unity Editor içinde variant comparison, preview ve assign workflow'u.
- Procedural content generation tarafına kontrollü ilk adım.
- Pipeline report summary panelleri.
- Audio analysis başlangıcı.
- Data-driven beatmap workflow.
- Dokümante edilmiş milestone geliştirme süreci.

Bu proje yalnızca bir Unity sahnesinden ibaret değildir. Runtime, tooling, veri akışı ve test
tarafları birlikte ele alınmış bir teknik prototiptir. Doğru sunulduğunda teknik görüşmelerde
mimari kararlar, veri pipeline’ı ve doğrulama yaklaşımı üzerinden güçlü tartışma zemini
sağlar.

---

## İlgili dokümanlar

- [docs/06-debug-prototype-milestone.md](docs/06-debug-prototype-milestone.md)
- [docs/07-audio-pipeline-milestone.md](docs/07-audio-pipeline-milestone.md)
- [docs/08-forge-preview-milestone.md](docs/08-forge-preview-milestone.md)
- [docs/09-combat-visualization-milestone.md](docs/09-combat-visualization-milestone.md)
- [docs/10-demo-recording-guide.md](docs/10-demo-recording-guide.md)
- [docs/11-repository-cleanup-checklist.md](docs/11-repository-cleanup-checklist.md)
- [docs/12-combat-style-variants-milestone.md](docs/12-combat-style-variants-milestone.md)
- [tools/audio_analyzer/README.md](tools/audio_analyzer/README.md)

---

## Kısa teknik özet

PulseForge şu an şunu kanıtlar:

```text
Ses dosyasından otomatik veya yarı otomatik ritim eventleri çıkarılabilir.
Bu eventler postprocess edilerek oynanabilir beatmap’e dönüştürülebilir.
Unity bu beatmap’i okuyup DSP audio clock ile senkron debug ritim-dövüş prototipinde oynatabilir.
Unity Editor tarafı raw/playable farkını timeline preview ve report summary panelleriyle görünür kılar.
Runtime prototype, hit/miss sonuçlarını OnGUI feedback'e ek olarak sahne üstünde parry/slash/hit taken feedback'ine dönüştürebilir.
BeatEvent intensity değeri sahne efektlerinin ölçek, parlaklık ve shake şiddetini etkileyebilir.
Aynı ritim analizinden Balanced, Defensive, Aggressive ve Bursty playable JSON varyantları üretilebilir ve Unity Editor içinde karşılaştırılıp prototype'a atanabilir.
```

Bu henüz final oyun değildir. Ama final oyuna doğru iyi kurulmuş bir temel ve gösterilebilir bir teknik prototiptir.

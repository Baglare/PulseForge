# PulseForge Milestone 2: Audio Pipeline Prototype

Bu belge, PulseForge projesinde **Milestone 2: Audio Pipeline Prototype** aşamasında tamamlanan işleri, mimari kararları, kullanım akışını, sınırlamaları ve sonraki adımları özetler.

Bu milestone'un amacı, bir ses dosyasından otomatik olarak oynanabilir ritim verisi üretmeye yönelik ilk uçtan uca hattı kurmaktır. Bu aşama final müzik analiz sistemi değildir. Ama artık proje yalnızca elle yazılmış beatmap ile çalışan bir debug prototip olmaktan çıkmış, ses dosyasından veri üretip Unity içinde oynatılabilen bir geliştirme aracına sahip olmuştur.

---

## 1. Milestone amacı

Bu milestone'da hedeflenen ana akış şudur:

```text
WAV audio file
    ↓
Python audio analyzer
    ↓
Raw beatmap JSON
    ↓
Playable beatmap postprocessor
    ↓
Playable beatmap JSON
    ↓
Unity Editor pipeline window
    ↓
DebugRhythmPrototypeController
    ↓
Playable rhythm-combat prototype
```

Bu akış sayesinde geliştirici Unity içinde bir WAV dosyası seçip Python tabanlı pipeline'ı çalıştırabilir, üretilen playable beatmap JSON dosyasını debug prototype sahnesine bağlayabilir ve sonucu Play Mode'da test edebilir.

Bu milestone'un ana fikri şudur:

> PulseForge artık yalnızca ritim olaylarını oynatan bir debug sahnesi değil; ses dosyasından ritim verisi üretmeye başlayan bir audio-to-gameplay pipeline prototipidir.

---

## 2. Tamamlanan ana parçalar

### Python audio analyzer araçları

Aşağıdaki araçlar `tools/audio_analyzer/` altında oluşturuldu:

```text
tools/audio_analyzer/
├── pulseforge_audio_analyzer.py
├── generate_debug_click_track.py
├── compare_beatmaps.py
├── postprocess_beatmap.py
├── run_debug_pipeline.py
├── README.md
└── tests/
    ├── test_pulseforge_audio_analyzer.py
    ├── test_compare_beatmaps.py
    ├── test_postprocess_beatmap.py
    └── test_run_debug_pipeline.py
```

Bu araçların tamamı ilk sürümde **Python standard library only** yaklaşımıyla yazıldı. Yani `numpy`, `librosa`, `scipy`, `ffmpeg`, `pydub` gibi dış bağımlılıklar kullanılmadı. Bu tercih, erken aşamada pipeline'ın daha kolay test edilmesini ve kurulumsuz çalışmasını sağlamak için yapıldı.

### Unity tarafı JSON import desteği

Unity runtime tarafında `DebugBeatMapJsonParser` eklendi. Bu sınıf schemaVersion 1 JSON beatmap dosyalarını okuyup domain tarafında kullanılan `BeatEventData` listesine dönüştürüyor.

Beatmap öncelik sırası:

```text
1. JSON TextAsset
2. DebugBeatMapAsset ScriptableObject
3. Hardcoded default beatmap
```

Bu sayede debug prototype artık hem elle düzenlenen ScriptableObject beatmap'i hem de dış araçlarla üretilen JSON beatmap'i kullanabiliyor.

### Unity Editor audio pipeline window

Unity Editor içine şu pencere eklendi:

```text
Tools > PulseForge > Audio Pipeline
```

Bu pencere Python tarafındaki `run_debug_pipeline.py` aracını Unity içinden çalıştırıyor. Geliştirici şu işlemleri Unity Editor üzerinden yapabiliyor:

- WAV AudioClip seçme
- Detection mode seçme
- Difficulty seçme
- Action mode ve pattern belirleme
- Expected beatmap ile karşılaştırma yapma
- Debug CSV üretme
- Pipeline'ı çalıştırma
- Generated playable JSON'u Project panelinde seçme
- Seçili `DebugRhythmPrototypeController` objesine generated JSON'u atama

Bu araç runtime build'e dahil değildir. Sadece geliştirme/editor workflow için kullanılır.

---

## 3. Python araçları

### 3.1 `generate_debug_click_track.py`

Bu araç bilinen zamanlarda click sesi içeren deterministic bir WAV dosyası üretir.

Örnek kullanım:

```powershell
python tools/audio_analyzer/generate_debug_click_track.py --output Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav --times 1.00,1.50,2.00,2.50,3.00,3.25,3.75,4.25,4.75,5.25
```

Varsayılanlar:

```text
sample rate: 44100 Hz
channels: mono
sample format: 16-bit PCM
click duration: 25 ms
click frequency: 1000 Hz
amplitude: 0.8
```

Bu aracın amacı gerçek müzik üretmek değil, analyzer ve Unity senkronizasyonunu test etmek için kontrollü ses verisi sağlamaktır.

---

### 3.2 `pulseforge_audio_analyzer.py`

Bu araç WAV dosyasından event zamanları çıkarır ve Unity uyumlu beatmap JSON üretir.

Desteklenen detection modları:

```text
amplitude
onset
```

#### Amplitude mode

Amplitude mode, frame bazlı ses genliğini inceler ve belirgin peak noktalarını event olarak seçer. Debug click track gibi net transient içeren seslerde başarılıdır.

#### Onset mode

Onset mode, doğrudan ses yüksekliğine değil, ses enerjisindeki ani artışa bakar. Gerçek müzik benzeri dosyalarda amplitude mode'a göre daha anlamlı sonuçlar verebilir.

Onset mode şu fikre dayanır:

```text
current frame amplitude
    - trailing baseline amplitude
    = onset strength
```

Bu, “ses ne kadar yüksek?” yerine “ses enerjisi burada ani şekilde arttı mı?” sorusuna cevap verir.

Örnek analyzer komutu:

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav --output Assets/PulseForge/Demo/BeatMaps/BM_Raw_Debug_120BPM.json --display-name "Raw Debug 120 BPM" --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike --detection-mode amplitude --summary
```

Diagnostics çıktıları:

```text
--report-output
--debug-csv-output
--summary
```

Report JSON analyzer ayarlarını, event sayısını, sample rate bilgisini ve seçilen eventleri içerir.

Debug CSV frame bazlı şu bilgileri içerir:

```text
frameIndex,timeSeconds,amplitude,onsetStrength,detectionValue,isLocalPeak,isSelectedPeak
```

Bu CSV, analyzer'ın hangi frame'leri peak olarak gördüğünü incelemek için kullanılır. Unity'ye verilmez.

---

### 3.3 `compare_beatmaps.py`

Bu araç iki beatmap JSON dosyasını karşılaştırır.

Örnek kullanım:

```powershell
python tools/audio_analyzer/compare_beatmaps.py Assets/PulseForge/Demo/BeatMaps/BM_Debug_120BPM_Default.json Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM.json --tolerance-ms 40 --report-output tools/audio_analyzer/out/compare_debug_120bpm.json
```

Karşılaştırma index bazlı yapılır:

```text
expected event 1 ↔ actual event 1
expected event 2 ↔ actual event 2
...
```

Raporlanan değerler:

```text
expectedEventCount
actualEventCount
comparedEventCount
missingEventCount
extraEventCount
actionMismatchCount
withinToleranceCount
outsideToleranceCount
meanSignedErrorMs
meanAbsoluteErrorMs
maxAbsoluteErrorMs
suggestedGlobalOffsetSeconds
```

`suggestedGlobalOffsetSeconds`, actual beatmap'in expected beatmap'e göre ne kadar kaydırılması gerektiğini gösterir.

Örnek yorum:

```text
meanSignedErrorMs = +10
suggestedGlobalOffsetSeconds = -0.010
```

Bu, actual eventlerin ortalama 10 ms geç olduğunu ve beatmap'in yaklaşık 10 ms erkene çekilebileceğini gösterir.

---

### 3.4 `postprocess_beatmap.py`

Analyzer output'u doğrudan oynanabilir olmak zorunda değildir. Gerçek müziklerde çok sık veya düzensiz eventler çıkabilir.

Bu araç raw beatmap JSON'u daha oynanabilir bir combat beatmap JSON'a dönüştürür.

Örnek kullanım:

```powershell
python tools/audio_analyzer/postprocess_beatmap.py Assets/PulseForge/Demo/BeatMaps/BM_Raw_Debug_120BPM.json --output Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM.json --display-name "Playable Debug 120 BPM" --difficulty hard --action-mode pattern --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike --report-output tools/audio_analyzer/out/postprocess_debug_120bpm_report.json
```

Difficulty presetleri:

```text
easy   → minGapSeconds = 0.45
normal → minGapSeconds = 0.28
hard   → minGapSeconds = 0.18
```

Default debug map içinde `3.00` ve `3.25` saniyelerinde iki event bulunur. Aralarındaki fark 250 ms olduğu için `normal` difficulty kullanılırsa biri drop edilebilir. Bu nedenle debug click track ile 10 eventin korunması isteniyorsa `hard` kullanmak daha uygundur.

Action mode seçenekleri:

```text
preserve
alternate
pattern
intensity
```

- `preserve`: Analyzer output actionlarını korur.
- `alternate`: Guard, Strike, Guard, Strike şeklinde sırayla dağıtır.
- `pattern`: Verilen pattern'i döngü halinde uygular.
- `intensity`: Intensity threshold üstündekileri Strike, altındakileri Guard yapar.

---

### 3.5 `run_debug_pipeline.py`

Bu araç tüm debug pipeline'ı tek komutta çalıştırır.

Örnek kullanım:

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

Unity'ye verilmesi gereken dosya:

```text
BM_Playable_Debug_120BPM.json
```

Raw beatmap ve report dosyaları debug/geliştirme içindir.

---

## 4. JSON beatmap formatı

Unity tarafındaki `DebugBeatMapJsonParser`, schemaVersion 1 formatını okur.

Örnek:

```json
{
  "schemaVersion": 1,
  "displayName": "Playable Debug 120 BPM",
  "globalOffsetSeconds": 0.0,
  "events": [
    {
      "eventId": "event-001",
      "targetTimeSeconds": 1.0,
      "action": "Guard",
      "intensity": 1.0
    },
    {
      "eventId": "event-002",
      "targetTimeSeconds": 1.5,
      "action": "Strike",
      "intensity": 1.0
    }
  ]
}
```

Alanlar:

```text
schemaVersion       → Şu an sadece 1 destekleniyor.
displayName         → Debug amaçlı görünen ad.
globalOffsetSeconds → Tüm eventlere uygulanacak genel offset.
events              → Oynanabilir ritim eventleri.
```

Event alanları:

```text
eventId             → Benzersiz event kimliği.
targetTimeSeconds   → Şarkı zamanında hedef saniye.
action              → Guard veya Strike.
intensity           → 0 ile 1 arasında vurgu gücü.
```

---

## 5. Unity Editor Audio Pipeline Window

Editor penceresi şu menüden açılır:

```text
Tools > PulseForge > Audio Pipeline
```

Penceredeki ana alanlar:

```text
Input Audio Clip
Expected Beat Map Json
Output Name
Pattern
Detection Mode
Difficulty
Action Mode
Write Debug CSV
Use Expected Compare
Python Executable
Output Directory
```

Pipeline çalıştırıldığında şu script çağrılır:

```text
tools/audio_analyzer/run_debug_pipeline.py
```

Başarılı çalıştırmadan sonra:

- AssetDatabase refresh edilir.
- Generated playable JSON pencerede gösterilir.
- İstenirse Project panelinde ping/select yapılabilir.
- İstenirse seçili `DebugRhythmPrototypeController` objesine JSON atanabilir.

Atama otomatik yapılmaz. Kullanıcı `Assign to Selected Debug Prototype` butonuna basmalıdır. Bu tercih, sahne dosyasının istemeden değişmesini önlemek için yapıldı.

---

## 6. Demo sahnesi kullanımı

Demo scene:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

Önerilen demo setup:

```text
Debug Audio Clip:
PF_Debug_120BPM_DefaultBeatMap.wav

Debug Beat Map Json:
BM_Playable_Debug_120BPM.json

Clock:
DspAudioSongClock
```

Play Mode testi:

```text
1. Start / Restart bas.
2. Countdown bitmesini bekle.
3. Lane markerları click sesleriyle akmalı.
4. Space = Guard.
5. J = Strike.
6. Perfect / Good / Miss feedbackleri görünmeli.
7. Score ve combo güncellenmeli.
8. Combat panelinde PARRY / SLASH / MISS feedbackleri görünmeli.
```

---

## 7. Test stratejisi

Python araçları `unittest` ile test edilir:

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

Bu milestone sırasında test kapsamı şunları içerir:

- WAV generator valid WAV üretiyor mu?
- Analyzer click track'ten beklenen event sayısını çıkarıyor mu?
- Amplitude ve onset mode schemaVersion 1 üretiyor mu?
- Diagnostics report ve CSV oluşuyor mu?
- Beatmap comparison timing hatalarını doğru hesaplıyor mu?
- Suggested global offset doğru yönde hesaplanıyor mu?
- Postprocessor min-gap filtreleme yapıyor mu?
- Action mode seçenekleri doğru çalışıyor mu?
- Pipeline runner raw/playable JSON üretiyor mu?

Unity tarafında manuel testler hâlâ gereklidir:

- EditorWindow açılıyor mu?
- Python process çalışıyor mu?
- Generated JSON Unity tarafından import ediliyor mu?
- Debug prototype sahnesinde oynatılabiliyor mu?
- DSP clock ile ses/lane akışı çalışıyor mu?

---

## 8. Bilinçli sınırlamalar

Bu milestone hâlâ final sistem değildir.

Bilinçli olarak yapılmayanlar:

```text
MP3 desteği yok.
Runtime file picker yok.
Gerçek zamanlı analiz yok.
librosa/numpy/scipy yok.
Makine öğrenmesi modeli yok.
Waveform UI yok.
Tam beatmap editor yok.
Seviye kalite puanlama sistemi yok.
Çok aşamalı combat pattern generator yok.
```

Analyzer şu an basit amplitude/onset yaklaşımı kullanır. Bu, click track ve belirgin transient içeren dosyalar için uygundur. Karmaşık müziklerde fazla event, eksik event veya ritim dışı peak seçimi olabilir.

Postprocessor bu problemi kısmen azaltır, fakat gerçek müziklerde daha iyi sonuç için ileride daha gelişmiş analiz ve pattern generation gerekecektir.

---

## 9. Portfolyo değeri

Bu milestone portfolyoda güçlü görünür çünkü yalnızca oyun sahnesi değil, bütün bir geliştirme hattı içerir:

```text
Python CLI tools
Unity JSON importer
Unity EditorWindow integration
Debug rhythm-combat runtime
Deterministic demo audio generation
Diagnostics / report / CSV
Beatmap comparison
Playable postprocessing
```

Bu, şu teknik kararları anlatma fırsatı verir:

- Domain logic ile Unity runtime ayrımı
- JSON data contract kullanımı
- Python toolchain ile Unity entegrasyonu
- Editor tooling geliştirme
- Audio analysis pipeline prototipleme
- Test edilebilir CLI araçları
- Raw data ile playable data ayrımı
- Diagnostics ve observability yaklaşımı

Kısa portfolyo cümlesi:

> PulseForge is a rhythm-combat prototype that includes a Python-based WAV analysis pipeline, JSON beatmap generation, Unity Editor integration, and a playable Unity debug scene using DSP-synchronized audio playback.

Türkçe açıklama:

> PulseForge, ses dosyasından ritim eventleri çıkarıp bunları oynanabilir combat beatmap'e dönüştüren; Python analiz araçları, Unity Editor pipeline penceresi ve DSP senkronizasyonlu debug oynanış sahnesi içeren bir ritim-dövüş prototipidir.

---

## 10. Sonraki adımlar

Önerilen sonraki teknik adımlar:

### 1. Analyzer quality improvements

Gerçek müzik dosyalarında daha iyi sonuç için:

```text
adaptive threshold
energy smoothing
beat density limiting
section detection
accent detection
```

### 2. Combat pattern generator

Postprocessor şu an temel action mapping yapıyor. Daha sonra şunlar eklenebilir:

```text
ParryDuel section
Offense section
Burst sequence
Breather section
combo grouping
intensity-based pattern selection
```

### 3. Unity visualization improvements

Debug OnGUI yerine daha temiz bir prototype UI:

```text
Canvas / UI Toolkit
clearer timing feedback
better lane visuals
simple character silhouettes
parry/slash animation placeholders
```

### 4. Audio import expansion

İleride:

```text
MP3 support
FFmpeg integration
librosa-based analyzer option
track package format
```

### 5. Documentation / README update

Ana README içine Milestone 2 özeti eklenmeli:

```text
Audio Pipeline Prototype completed
How to run pipeline from Unity
How to run Python tools manually
Known limitations
Roadmap
```

---

## 11. Milestone kabul ölçütleri

Bu milestone aşağıdaki koşullar sağlandığında tamamlanmış sayılır:

```text
[✓] Python debug click track generator çalışıyor.
[✓] Python WAV analyzer JSON beatmap üretiyor.
[✓] Amplitude ve onset detection modları mevcut.
[✓] Diagnostics report ve debug CSV üretilebiliyor.
[✓] Beatmap comparison CLI timing farklarını ölçebiliyor.
[✓] Postprocessor raw beatmap'i playable beatmap'e dönüştürebiliyor.
[✓] Pipeline runner tek komutla raw/playable JSON üretebiliyor.
[✓] Unity Editor Audio Pipeline Window Python runner'ı çalıştırabiliyor.
[✓] Generated playable JSON Unity Project panelinde görünür hale geliyor.
[✓] Generated JSON seçili DebugRhythmPrototypeController'a atanabiliyor.
[✓] Demo scene generated playable JSON ile oynatılabiliyor.
[✓] Python unittest suite geçiyor.
```

Bu aşamadan sonra proje, ses analizinden oynanabilir ritim-combat prototipine uzanan ilk çalışır pipeline'a sahiptir.

# PulseForge

PulseForge, zamanlanmış ritim olaylarını oynanabilir bir kılıç dövüşü etkileşimine dönüştüren deneysel bir rhythm-combat prototipidir. Mevcut sürüm henüz şarkıları otomatik analiz etmez. Bu aşamanın amacı; timing judgement, input eşleştirme, skor, combo, timeout miss, audio-senkron zamanlama ve debug combat feedback gibi temel runtime sistemlerinin çalıştığını kanıtlamaktır.

Proje, portfolyo odaklı bir Unity projesi olarak geliştiriliyor. Ana hedef, tek parça gizemli bir kod yığını üretmek yerine açık mimari, test edilebilir domain mantığı ve küçük milestone’larla ilerlemek. Yani evet, bu kez “çalışıyor ama kimse nedenini bilmiyor” geleneğine katılmıyoruz.

## Mevcut durum

**Milestone 1: Debug Rhythm Combat Prototype** tamamlandı.

Bu prototip şu özellikleri destekler:

- Guard ve Strike ritim eventleri.
- Perfect, Good ve Miss timing değerlendirmesi.
- Pending, Hit ve Missed runtime event durumları.
- Deterministik input-event eşleştirme.
- Oyuncu zamanında basmazsa timeout tabanlı Miss işleme.
- Score, combo, Perfect, Good ve Miss sayaçları.
- `OnGUI` ile çizilen debug rhythm lane.
- Parry, slash ve miss feedback için debug combat paneli.
- `AudioSettings.dspTime` ve `AudioSource.PlayScheduled` ile opsiyonel DSP tabanlı audio timing.
- `DebugBeatMapAsset` ile Inspector’dan düzenlenebilir beatmap verisi.

Bu milestone bilinçli olarak debug prototiptir. Final UI değildir, otomatik ses analizi değildir ve tamamlanmış dövüş sunumu değildir.

## Demo scene

Unity içinde şu sahneyi aç:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

Beklenen scene objesi:

```text
Debug Rhythm Prototype
```

Beklenen component:

```text
DebugRhythmPrototypeController
```

Önerilen Inspector ayarı:

```text
Debug Audio Clip: PF_Debug_120BPM_DefaultBeatMap.wav
Use Audio Clock When Clip Assigned: true
Debug Beat Map Asset: BM_Debug_120BPM_Default
```

## Demo nasıl çalıştırılır?

1. Demo scene’i aç:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

2. Play Mode’a gir.
3. Debug UI içinden `Start / Restart` butonuna bas.
4. Aşağıdaki kontrolleri kullan:

| Action | Klavye | GUI |
|---|---|---|
| Guard | `Space` | `Guard` butonu |
| Strike | `J` | `Strike` butonu |

5. Rhythm lane üzerindeki markerların hit line’a yaklaşmasını izle.
6. Doğru action’ı hit line’a yakın zamanda kullan.
7. Timing feedback, score, combo, event state ve combat feedback değerlerini gözlemle.

Olası feedback örnekleri:

```text
PERFECT PARRY
GOOD PARRY
PERFECT SLASH
GOOD SLASH
MISS / HIT TAKEN
```

## Demo assetleri

Beklenen demo klasör yapısı:

```text
Assets/PulseForge/Demo/
├── Audio/
├── BeatMaps/
└── Scenes/
```

Örnek audio clip:

```text
Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav
```

Örnek beatmap asset:

```text
Assets/PulseForge/Demo/BeatMaps/BM_Debug_120BPM_Default.asset
```

Default debug beat eventleri:

| Event ID | Time | Action | Intensity |
|---|---:|---|---:|
| `event-001` | 1.00 | Guard | 1.0 |
| `event-002` | 1.50 | Guard | 1.0 |
| `event-003` | 2.00 | Strike | 1.0 |
| `event-004` | 2.50 | Guard | 1.0 |
| `event-005` | 3.00 | Strike | 1.0 |
| `event-006` | 3.25 | Strike | 1.0 |
| `event-007` | 3.75 | Guard | 1.0 |
| `event-008` | 4.25 | Strike | 1.0 |
| `event-009` | 4.75 | Guard | 1.0 |
| `event-010` | 5.25 | Strike | 1.0 |

## Mimari özeti

PulseForge şu anda iki ana katmana ayrılır.

### Domain katmanı

Domain katmanı saf C# ritim mantığını içerir. Unity sahnesine, `MonoBehaviour` sınıflarına, `AudioSource` sistemine, input API’lerine, UI’a veya `UnityEngine` bağımlılığına dayanmaz.

| Class | Sorumluluk |
|---|---|
| `BeatEventData` | Değişmeyen beat event verisini temsil eder. |
| `JudgementWindows` | Perfect ve Good timing aralıklarını tutar. |
| `HitJudge` | Input zamanlamasını Perfect, Good veya Miss sonucuna çevirir. |
| `HitResult` | Timing değerlendirme sonucunu taşır. |
| `BeatEventRuntime` | Session sırasında eventin Pending, Hit veya Missed durumunu tutar. |
| `BeatEventMatcher` | Bir input için en uygun pending event’i bulur. |
| `RhythmInputResolver` | Matching, judgement ve runtime state uygulamasını birleştirir. |
| `BeatEventTimeoutProcessor` | Süresi geçmiş pending eventleri Missed yapar. |
| `RhythmSession` | Runtime event listesini yönetir ve input/timeout işlemlerini dışarı sunar. |
| `ScoreTracker` | Score, combo, Perfect, Good ve Miss sayaçlarını takip eder. |
| `ScoreSnapshot` | Score durumunu read-only snapshot olarak dışarı taşır. |

### Unity runtime / debug katmanı

Unity katmanı, domain modelini Play Mode’a, debug görselleştirmeye, zaman kaynaklarına ve authoring assetlerine bağlar.

| Class | Sorumluluk |
|---|---|
| `ISongClock` | Şarkı zamanı sağlayıcıları için ortak interface. |
| `RealtimeSongClock` | Unity realtime tabanlı debug clock. |
| `DspAudioSongClock` | `AudioSettings.dspTime` tabanlı audio-senkron clock. |
| `DebugBeatMapAsset` | Inspector’dan düzenlenebilir beatmap veri kaynağı. |
| `DebugRhythmPrototypeController` | Play Mode debug prototipini çalıştırır ve OnGUI debug UI çizer. |

## Runtime akışı

```text
Start / Restart
    ↓
Clock seçimi
    ├── AudioClip atanmış → DspAudioSongClock
    └── AudioClip yok     → RealtimeSongClock
    ↓
Beatmap kaynağı seçimi
    ├── DebugBeatMapAsset atanmış → asset eventleri
    └── Asset yok                 → default hardcoded eventler
    ↓
RhythmSession oluşturulur
    ↓
ScoreTracker oluşturulur
    ↓
Her Update current song time okur
    ↓
Süresi geçen eventler Missed olur
    ↓
OnGUI Guard / Strike inputlarını işler
    ↓
RhythmSession inputu çözer
    ↓
ScoreTracker HitResult kaydeder
    ↓
Rhythm lane ve combat feedback güncellenir
```

## Testler

Domain katmanı Unity Edit Mode testleriyle korunur. Bu testler timing judgement, runtime state geçişleri, event matching, timeout miss, session completion ve score tracking davranışlarını doğrular.

Test edilen başlıca alanlar:

- Perfect, Good ve Miss timing sınırları.
- Geçersiz veri doğrulama.
- Pending, Hit ve Missed event state geçişleri.
- En iyi event eşleştirme ve deterministik tie-break kuralları.
- Timeout tabanlı Miss işleme.
- RhythmSession sayaçları ve completion state.
- Score, combo, duplicate result koruması ve reset davranışı.
- Domain assembly’nin `UnityEngine` bağımlılığından izole kalması.

Testleri çalıştırmak için:

```text
Window > General > Test Runner > EditMode > Run All
```

Unity runtime debug prototipi şu anda manuel Play Mode kontrolleriyle doğrulanır.

## Bu proje şu anda ne yapmıyor?

Aşağıdaki özellikler bilinçli olarak mevcut milestone kapsamının dışındadır:

- Otomatik şarkı analizi.
- MP3/WAV file picker.
- Python audio analysis pipeline.
- Otomatik beatmap üretimi.
- Waveform preview.
- JSON beatmap import/export.
- Final combat görselleri.
- Sprite animasyonu veya karakter art’ı.
- Production UI.
- Pause/resume akışı.
- Difficulty profiles.
- Latency calibration.

Bunlar unutulmuş özellikler değil, gelecek milestone adaylarıdır. Aradaki fark küçük görünür ama proje yönetimi denen şey o küçük farkların üstünde hayatta kalır.

## Roadmap

Önerilen sonraki adımlar:

1. `DebugRhythmPrototypeController` sınıfını daha küçük presenter/helper parçalara ayır.
2. Daha temiz bir demo UI veya minimal Canvas tabanlı prototip ekle.
3. OnGUI combat feedback yerine basit sprite veya çizilmiş combat görselleri kullan.
4. Beatmap verisi için JSON import/export ekle.
5. İlk Python audio analysis pipeline’ını kur.
6. Audio’dan beat time değerlerini çıkar.
7. Analiz çıktısını oynanabilir `BeatEventData` verisine çevir.
8. Waveform ve beat marker preview ekle.
9. Analiz edilen müzik bölümlerinden combat pattern üret.

Bir sonraki büyük teknik faz:

```text
Audio file → beat analysis → beat event data → RhythmSession
```

## Portfolyo değeri

PulseForge şu anda şunları gösterir:

- Aşamalı oyun sistemi geliştirme.
- Test edilebilir ritim judgement mantığı.
- Domain logic ve Unity runtime kodunun ayrılması.
- Deterministik input-event matching.
- Time-based state transition.
- Score ve combo tracking.
- DSP tabanlı audio clock entegrasyonu.
- ScriptableObject ile data-driven beatmap authoring.
- Final görsellerden önce debug-first prototyping yaklaşımı.

Proje hâlâ erken aşamada, fakat otomatik audio analysis ve üretilmiş combat choreography yönüne genişleyebilecek çalışan bir rhythm-combat runtime temeli oluşmuş durumda.

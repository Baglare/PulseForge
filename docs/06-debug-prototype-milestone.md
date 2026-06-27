# PulseForge Debug Rhythm Combat Prototype Milestone

## 1. Milestone amacı

Bu milestone, PulseForge'un ilk oynanabilir debug prototipini oluşturur. Amaç final oyun görünümü üretmek değil; ritim domain çekirdeğinin Unity içinde gerçek bir oyun akışına bağlanabildiğini kanıtlamaktır.

Bu aşamada sistem şunları gösterebilir:

- Sabit veya Inspector üzerinden düzenlenebilir beat event listesiyle ritim oturumu başlatma.
- Guard ve Strike inputlarını zamanlamaya göre değerlendirme.
- Perfect, Good ve Miss sonuçlarını üretme.
- Score, combo ve sonuç sayaçlarını güncelleme.
- Kaçırılan eventleri zaman aşımıyla Missed durumuna geçirme.
- Rhythm lane üzerinde yaklaşan eventleri gösterme.
- Basit combat feedback ile parry, slash ve miss hissi verme.
- AudioClip atanmışsa DSP tabanlı audio clock ile çalışma.

Bu milestone, ileride eklenecek audio analysis ve otomatik beatmap üretimi için çalışır bir runtime zemin sağlar.

## 2. Bu milestone'da tamamlanan ana parçalar

### Domain katmanı

Domain katmanı Unity sahnesinden, input sisteminden, audio sisteminden ve görsel arayüzden bağımsızdır.

| Sınıf | Sorumluluk |
|---|---|
| `BeatEventData` | Haritadaki sabit ritim olayını temsil eder. |
| `JudgementWindows` | Perfect ve Good zaman pencerelerini tutar. |
| `HitJudge` | Input zamanını hedef event zamanına göre Perfect, Good veya Miss olarak değerlendirir. |
| `HitResult` | Bir judgement sonucunu taşır. |
| `BeatEventRuntime` | Bir eventin oturum sırasındaki Pending, Hit veya Missed durumunu tutar. |
| `BeatEventMatcher` | Input geldiğinde en uygun pending event'i seçer. |
| `RhythmInputResolver` | Matcher, judge ve runtime state update zincirini yürütür. |
| `BeatEventTimeoutProcessor` | Good window süresi geçmiş pending eventleri Missed yapar. |
| `RhythmSession` | Bir ritim oturumunun eventlerini, input çözümlemeyi ve timeout işlemlerini yönetir. |
| `ScoreTracker` | Skor, combo, Perfect, Good ve Miss sayaçlarını takip eder. |
| `ScoreSnapshot` | ScoreTracker durumunu dışarı taşır. |

### Unity runtime / debug katmanı

| Sınıf | Sorumluluk |
|---|---|
| `ISongClock` | Şarkı zamanı sağlayan ortak arayüz. |
| `RealtimeSongClock` | Audio olmadan debug amaçlı zaman üretir. |
| `DspAudioSongClock` | `AudioSettings.dspTime` ve `AudioSource.PlayScheduled` ile audio tabanlı zaman üretir. |
| `DebugBeatMapAsset` | Inspector'dan düzenlenebilir debug beatmap asset'i sağlar. |
| `DebugRhythmPrototypeController` | Domain sistemini Unity Play Mode'da çalıştırır ve OnGUI debug arayüzü gösterir. |

## 3. Demo scene

Demo scene yolu:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

Scene içinde beklenen ana obje:

```text
Debug Rhythm Prototype
```

Bu objede şu component bulunmalıdır:

```text
DebugRhythmPrototypeController
```

Inspector'da önerilen ayarlar:

```text
Debug Audio Clip: PF_Debug_120BPM_DefaultBeatMap.wav
Use Audio Clock When Clip Assigned: true
Debug Beat Map Asset: BM_Debug_120BPM_Default
```

## 4. Demo assetleri

Beklenen demo klasörleri:

```text
Assets/PulseForge/Demo/
├── Audio/
├── BeatMaps/
└── Scenes/
```

Örnek audio dosyası:

```text
Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav
```

Örnek beatmap asset'i:

```text
Assets/PulseForge/Demo/BeatMaps/BM_Debug_120BPM_Default.asset
```

Örnek event zamanları:

| EventId | Time | Action | Intensity |
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

## 5. Nasıl çalıştırılır?

1. Unity'de şu scene'i aç:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

2. Play Mode'a gir.
3. Game penceresinde `Start / Restart` butonuna bas.
4. Guard için `Space` veya GUI üzerindeki `Guard` butonunu kullan.
5. Strike için `J` veya GUI üzerindeki `Strike` butonunu kullan.
6. Rhythm lane üzerinde markerların hit line'a yaklaşmasını izle.
7. Score, combo, feedback ve event state listesini kontrol et.

Beklenen feedback örnekleri:

```text
PERFECT PARRY
GOOD PARRY
PERFECT SLASH
GOOD SLASH
MISS / HIT TAKEN
```

## 6. Temel runtime akışı

```text
Start / Restart
    ↓
Clock seçimi
    ├── AudioClip varsa DspAudioSongClock
    └── AudioClip yoksa RealtimeSongClock
    ↓
BeatMap seçimi
    ├── DebugBeatMapAsset varsa asset eventleri
    └── Yoksa default hardcoded eventler
    ↓
RhythmSession oluşturulur
    ↓
ScoreTracker oluşturulur
    ↓
Update içinde current time okunur
    ↓
Timeout olan eventler Missed yapılır
    ↓
OnGUI input gelirse ResolveInput çağrılır
    ↓
HitResult ScoreTracker'a kaydedilir
    ↓
Rhythm lane ve combat feedback çizilir
```

## 7. Test stratejisi

Bu milestone iki tür doğrulama içerir.

### Edit Mode testleri

Domain davranışları Edit Mode testleriyle doğrulanır:

- Perfect, Good ve Miss sınırları.
- Runtime event state geçişleri.
- Pending event matching.
- Timeout ile Missed geçişi.
- Session sayaçları ve tamamlanma durumu.
- Score ve combo hesaplama.
- Domain assembly'nin `UnityEngine` bağımlılığı almaması.

### Manual Play Mode kontrolü

Unity runtime tarafı manuel olarak kontrol edilir:

- Debug scene açılır.
- Start / Restart çalışır.
- Clock tipi doğru görünür.
- AudioClip atanmışsa `DspAudioSongClock` kullanılır.
- Lane markerları akış gösterir.
- Guard / Strike inputları sonuç üretir.
- Score ve combo değişir.
- Timeout Miss çalışır.
- Combat feedback görünür.

## 8. Bilinçli sınırlamalar

Bu milestone'da özellikle yapılmayanlar:

- Gerçek şarkı analizi.
- MP3/WAV dosya seçici.
- Python analiz pipeline'ı.
- Otomatik beatmap generation.
- Waveform görüntüleme.
- Canvas, TextMeshPro veya final UI.
- Sprite, animasyon veya gerçek dövüş karakterleri.
- Pause/resume sistemi.
- Zorluk profilleri.
- JSON import/export.
- Kullanıcı ayarlı latency/offset kalibrasyonu.

Bu sınırlamalar bilinçlidir. Amaç final oyunu tamamlamak değil, ritim runtime çekirdeğini kontrollü biçimde kanıtlamaktır.

## 9. Portfolyo değeri

Bu milestone portfolyoda şu konuları göstermek için değerlidir:

- Domain-driven küçük oyun sistemi tasarımı.
- Unity'den bağımsız test edilebilir ritim judgement çekirdeği.
- Deterministik input-event matching.
- Time-based state transition.
- Score ve combo sistemi.
- Unity runtime ile domain katmanının ayrılması.
- Debug prototip üzerinden hızlı doğrulama.
- DSP tabanlı audio clock kullanımına hazırlık.
- Data-driven beatmap asset yaklaşımı.

Bu çalışma, PulseForge'un ileride otomatik audio analysis tarafına bağlanabilecek sağlam bir runtime temelinin olduğunu gösterir.

## 10. Sonraki adımlar

Önerilen sonraki geliştirme sırası:

1. `DebugRhythmPrototypeController` kodunu küçük presenter/helper parçalara bölmek.
2. Daha düzgün bir demo UI veya minimal Canvas tabanlı prototip oluşturmak.
3. Debug combat feedback'i basit sprite veya çizim tabanlı görsele taşımak.
4. Beatmap verisi için JSON import/export hazırlamak.
5. Python tarafında ilk audio analysis pipeline'ını kurmak.
6. Audio analysis çıktısından beat event listesi üretmek.
7. Waveform ve beat marker preview ekranı tasarlamak.
8. Analizden gelen eventleri oynanabilir combat pattern'lara çevirmek.

Bir sonraki büyük teknik faz:

```text
Audio file → beat analysis → beat event data → RhythmSession
```

## 11. Milestone kabul ölçütleri

Bu milestone tamamlanmış sayılırsa aşağıdakiler doğru olmalıdır:

- Edit Mode testleri geçer.
- Demo scene Play Mode'da açılır.
- Start / Restart oturumu başlatır.
- DSP audio clock, AudioClip atanmışsa çalışır.
- DebugBeatMapAsset eventleri session'a aktarılır.
- Rhythm lane markerları zamana göre hareket eder.
- Guard / Strike inputları Perfect, Good veya Miss üretir.
- Score ve combo güncellenir.
- Timeout Miss çalışır.
- Combat feedback paneli sonuçları gösterir.
- Domain katmanı UnityEngine bağımlılığı almaz.

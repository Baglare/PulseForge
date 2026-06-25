# PulseForge Mimari Taslağı v0.1

## 1. Ana ayrım

PulseForge üç bağımsız problem olarak ele alınır:

1. **Audio Analysis:** Müziğin ritmik özelliklerini çıkarır.
2. **Combat Generation:** Analiz verisini oynanabilir dövüş olaylarına çevirir.
3. **Rhythm Runtime:** Olayları müzikle senkron biçimde oynatır ve girdileri değerlendirir.

Bu ayrım kritik bir tasarım kararıdır. Analiz katmanı `parry` kavramını bilmez. Oynanış katmanı da librosa veya başka bir analiz aracını bilmez.

## 2. Genel veri akışı

```text
Audio File
   |
   v
Python Audio Analyzer
   |
   v
TrackAnalysis JSON
   |
   v
Unity CombatBeatMapGenerator
   |
   v
BeatMapData
   |
   v
Unity Rhythm Runtime
   |
   v
HitResult + Combat Presentation + SessionResult
```

## 3. Katmanlar

### 3.1 Domain

Saf kurallar ve veri kavramları burada bulunur.

Örnekler:

- `BeatEventData`
- `JudgementWindows`
- `HitResult`
- `HitJudge`
- Daha sonra `BeatMapData` ve üretim kuralları

Kurallar:

- `MonoBehaviour` kullanılmaz.
- Mümkünse `UnityEngine` referansı kullanılmaz.
- Sahne, prefab, input cihazı veya animasyon bilgisi taşımaz.
- Birim testleri Edit Mode'da hızlı çalışır.

### 3.2 Application

Use-case akışlarını koordine eder.

Örnekler:

- `RhythmSessionController`
- `BeatEventScheduler`
- `CombatBeatMapGenerator`
- `TrackImportController`

Bu katman Domain kurallarını kullanır, fakat görsel ayrıntıları doğrudan yönetmez.

### 3.3 Infrastructure

Dosya sistemi, JSON, Python süreci ve Unity ses sistemi gibi dış ayrıntıları içerir.

Örnekler:

- `LocalProcessAnalyzerClient`
- `TrackPackageRepository`
- `DspSongClock`
- JSON serializer adaptörleri

### 3.4 Presentation

Sahne, animasyon, ses efekti, kamera ve kullanıcı arayüzünü içerir.

Örnekler:

- `PlayerCombatView`
- `EnemyCombatView`
- `CombatFeedbackPresenter`
- `ForgePreviewPresenter`
- `ResultsPresenter`

## 4. Bağımlılık yönü

```text
Presentation ------> Application ------> Domain
Infrastructure ----> Application ------> Domain
```

Domain dış katmanları bilmez. Bu sayede oyun kuralları sahne açmadan test edilebilir.

## 5. Başlangıç Unity klasör yapısı

```text
Assets/
└── PulseForge/
    ├── Runtime/
    │   ├── Domain/
    │   │   └── Rhythm/
    │   ├── Application/
    │   ├── Infrastructure/
    │   └── Presentation/
    ├── Tests/
    │   ├── EditMode/
    │   └── PlayMode/
    ├── Scenes/
    ├── Art/
    ├── Audio/
    └── Settings/
```

İlk Codex görevi yalnızca `Runtime/Domain/Rhythm` ve `Tests/EditMode` alanlarına dokunmalıdır.

## 6. Uzun vadeli Python yapısı

```text
analyzer/
├── pulseforge_analyzer/
│   ├── pipeline.py
│   ├── decoding.py
│   ├── rhythm.py
│   ├── intensity.py
│   ├── segmentation.py
│   ├── waveform.py
│   ├── validation.py
│   └── package_writer.py
└── tests/
```

Python analizcisi ilk geliştirme aşamasında oluşturulmayacaktır. Önce Unity ritim çekirdeği doğrulanır.

## 7. Veri sözleşmeleri

### TrackAnalysis

Oyundan bağımsız müzik analizi verisidir.

- track kimliği
- süre
- tahmini BPM
- ritim noktaları
- vurgu güçleri
- yoğunluk eğrisi
- bölüm listesi
- waveform önizlemesi
- analiz sürümü ve güven bilgisi

### BeatMapData

Oynanabilir dövüş haritasıdır.

- track kimliği
- zorluk profili
- seed
- generator sürümü
- combat phase listesi
- beat event listesi

### BeatEventData

Tek bir hedef oyuncu eylemidir.

- benzersiz olay kimliği
- hedef zaman
- mantıksal eylem
- yoğunluk
- phase kimliği
- isteğe bağlı sequence kimliği

## 8. Zaman mimarisi

Olaylar bir önceki olaydan geçen süreyle değil, şarkı başlangıcından itibaren mutlak saniye değeriyle tutulur.

```text
songTime = currentDspTime - scheduledSongStartDspTime + userOffset
```

İlk domain görevinde gerçek DSP saati yazılmayacaktır. Önce yalnızca verilen `inputTime` ve `targetTime` değerlerinin değerlendirilmesi kurulacaktır.

## 9. Değişiklik kuralları

- Bir sınıf iki farklı nedenden değişiyorsa sorumluluğu yeniden değerlendirilir.
- `Manager` adı varsayılan çözüm değildir.
- Global singleton kullanılmaz.
- Kod üretim aracı gereksiz paket ekleyemez.
- Her görev küçük, gözden geçirilebilir ve test edilebilir tutulur.
- Yeni soyutlama yalnızca gerçek bir bağımlılık sınırı veya test ihtiyacı varsa eklenir.

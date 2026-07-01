# PulseForge Milestone 4: Combat Visualization Prototype

Bu belge, PulseForge projesinde **Milestone 4: Combat Visualization Prototype** kapsamında eklenen sahne tabanlı debug combat feedback katmanını açıklar.

Bu milestone final art, final animasyon veya gerçek karakter sistemi değildir. Amaç, ritim sonucunun Unity sahnesinde okunabilir bir dövüş feedback'ine dönüşebildiğini göstermek ve bunu mevcut domain, scoring, judgement, Python analyzer ve Editor pipeline mimarisini bozmadan yapmaktır.

## 1. Milestone amacı

Milestone 4'ün amacı, Milestone 1'deki OnGUI combat feedback'ini kaldırmadan onun yanına sahne üzerinde basit 2D combat visualization katmanı eklemektir.

Bu aşamada sistem şunu kanıtlar:

- `Guard` sonucu sahnede parry feedback'e dönüşebilir.
- `Strike` sonucu sahnede slash feedback'e dönüşebilir.
- `Miss` ve timeout miss sonucu player tarafında hit taken feedback'e dönüşebilir.
- `Perfect` ve `Good` sonucu aynı gameplay kuralını kullanır, fakat görsel şiddet farkı yaratır.
- `BeatEventData.Intensity` değeri efekt ölçeği, parlaklığı ve miss shake şiddetine yansıtılabilir.

Bu, ritim verisi ile sahne sunumu arasındaki ilk okunabilir bağlantıdır. Hedef final görsel kalite değil, ritim sonucunun oyun hissine nasıl çevrileceğini erken ve test edilebilir şekilde göstermektir.

## 2. Bu milestone'da eklenen ana parçalar

Ana ekleme runtime Unity prototype katmanındadır:

| Parça | Sorumluluk |
|---|---|
| `DebugCombatSceneView` | Sahnede player/enemy primitive görsellerini ve parry/slash/miss efektlerini yönetir. |
| `DebugRhythmPrototypeController` bağlantısı | Hit/miss sonucunu ve event intensity değerini sahne feedback katmanına iletir. |

Mevcut OnGUI feedback sistemi korunmuştur:

- `DebugCombatFeedbackRenderer` halen OnGUI panelde `PERFECT PARRY`, `GOOD PARRY`, `PERFECT SLASH`, `GOOD SLASH` ve `MISS / HIT TAKEN` mesajlarını gösterir.
- `DebugCombatSceneView` buna ek olarak sahne içinde görsel feedback verir.
- Score, combo, judgement, timeout, countdown, ambiguous input ve clock davranışı değiştirilmemiştir.

## 3. DebugCombatSceneView

`DebugCombatSceneView`, `Assets/PulseForge/Runtime/Unity/Prototype/` altında bulunan bir `MonoBehaviour` sınıfıdır.

Sorumluluğu sadece görseldir:

- Sahnede basit `Player` ve `Enemy` görsellerini oluşturur veya mevcut child objeleri yönetir.
- `ParryEffect`, `SlashEffect` ve `FeedbackText` child objeleriyle kısa süreli feedback gösterir.
- Runtime'da küçük beyaz sprite oluşturabilir.
- Harici sprite, texture, material asset veya Animator Controller kullanmaz.
- Gameplay judgement hesaplamaz.
- Score veya event state değiştirmez.

Public API özeti:

```csharp
public void ResetView();
public void ShowHit(RhythmAction action, HitGrade grade);
public void ShowHit(RhythmAction action, HitGrade grade, float intensity);
public void ShowMiss();
public void ShowMiss(float intensity);
```

Eski overload'lar korunmuştur. Bu sayede sahne feedback katmanı eski çağrı şekillerini kırmadan intensity destekli hale gelmiştir.

## 4. Sahne feedback türleri

### Parry

`Guard` inputu bir event ile `Perfect` veya `Good` olarak eşleştiğinde player yakınında parry spark efekti gösterilir.

Parry feedback:

- Player tarafında görünür.
- Birden fazla küçük primitive çizgi/kutu ile spark hissi verir.
- `PERFECT PARRY` veya `GOOD PARRY` text'i gösterir.
- Event intensity yüksekse daha büyük ve parlak görünür.

### Slash

`Strike` inputu bir event ile `Perfect` veya `Good` olarak eşleştiğinde enemy üzerinde diagonal slash feedback'i gösterilir.

Slash feedback:

- Enemy üzerinde diagonal çizgi şeklinde görünür.
- Core ve glow benzeri primitive katmanlarla daha okunabilir hale getirilir.
- Enemy kısa süre slash rengine doğru flash yapar.
- `PERFECT SLASH` veya `GOOD SLASH` text'i gösterir.
- Event intensity yüksekse slash daha büyük ve daha belirgin görünür.

### Miss / Hit Taken

Input miss veya timeout miss sonucunda player tarafında hit taken feedback'i gösterilir.

Miss feedback:

- Player miss rengine doğru flash yapar.
- Player kısa süre hafif geri offset/shake hareketi alır.
- `MISS / HIT TAKEN` text'i gösterir.
- Intensity varsa flash ve shake şiddeti buna göre değişebilir.
- Efekt süresi bitince player normal pozisyon ve renge döner.

## 5. Perfect / Good farkının görsel karşılığı

Perfect ve Good farkı gameplay tarafında zaten `HitGrade` ile temsil edilir. Milestone 4'te bu fark sahne feedback'ine yalnızca görsel şiddet olarak yansıtılır.

- `Perfect` daha büyük ve daha parlak efekt kullanır.
- `Good` daha sakin bir efekt kullanır.
- Judgement pencereleri, score, combo veya runtime state davranışı değişmez.

Bu ayrım önemlidir: Sunum katmanı oyuncuya daha iyi his verirken, domain kuralları aynı kalır.

## 6. BeatEvent intensity değerinin görsele bağlanması

`BeatEventData` içindeki `Intensity` değeri `0-1` aralığında tutulur. Python analyzer ve postprocessor output'ları da bu değeri JSON beatmap içinde taşıyabilir.

Milestone 4 ile intensity, sahne feedback'inde şu alanlara bağlandı:

- Parry spark ölçeği.
- Parry/slash parlaklığı.
- Slash line ve glow ölçeği.
- Enemy flash şiddeti.
- Miss flash ve player shake mesafesi.

Bu bağlantı önemlidir çünkü her ritim eventi aynı ağırlıkta değildir. Daha güçlü vurguların sahnede daha güçlü okunması, audio analysis tarafından üretilen verinin sadece zamanlama değil, oyun hissi için de kullanılabileceğini gösterir.

Bu davranış sadece görseldir. Intensity score, judgement, combo veya event state hesaplamasını değiştirmez.

## 7. DebugRhythmPrototypeController ile bağlantı

`DebugRhythmPrototypeController`, ritim oturumunu yönetmeye devam eder. Sahne feedback katmanı bu controller tarafından beslenir.

Akış:

```text
Input
  ↓
RhythmSession.ResolveInput
  ↓
HitResult + MatchedEvent
  ↓
OnGUI combat feedback
  ↓
DebugCombatSceneView scene feedback
```

Match olan inputlarda `MatchedEvent.Data.Intensity` değeri `DebugCombatSceneView` tarafına iletilir.

Timeout miss durumunda, mümkün olduğunda `HitResult.EventId` üzerinden session event'i bulunur ve ilgili intensity değeri miss feedback'e aktarılır. Event bulunamazsa sistem eski davranışa yakın default miss feedback ile çalışır.

`DebugCombatSceneView` atanmamışsa controller hata vermez; OnGUI feedback ile debug prototype çalışmaya devam eder.

## 8. Nasıl test edilir?

Unity içinde manuel test adımları:

1. Demo sahneyi aç:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

2. Sahnede `DebugCombatSceneView` component'inin bulunduğunu kontrol et.
3. Play Mode'a gir.
4. `Start / Restart` butonuna bas.
5. Countdown bitince lane üzerindeki eventleri takip et.
6. `Space` ile `Guard` inputunu test et.
7. `J` ile `Strike` inputunu test et.
8. Bilerek input kaçırarak veya yanlış zamanda basarak miss feedback'i kontrol et.
9. Düşük ve yüksek intensity eventlerde efekt şiddeti farkını gözlemle.
10. OnGUI combat feedback panelinin hâlâ çalıştığını kontrol et.

Beklenen sonuçlar:

- Guard Perfect/Good sonucunda parry spark görünür.
- Strike Perfect/Good sonucunda slash feedback görünür.
- Miss veya timeout sonucunda player hit taken feedback görünür.
- Efektler kısa süre sonra temizlenir.
- Player ve enemy normal renk/pozisyonlarına döner.
- Score, combo, countdown, timing calibration, JSON beatmap ve DSP audio clock davranışı bozulmaz.

Edit Mode testleri için:

```text
Window > General > Test Runner > EditMode > Run All
```

Bu milestone yeni domain davranışı eklemediği için ana doğrulama Play Mode smoke test ve mevcut testlerin regresyon kontrolüdür.

## 9. Runtime / domain / Python / editor ayrımı

Milestone 4 yalnızca Unity runtime prototype sunum katmanını genişletir.

Değiştirilmemesi gereken ve korunmuş mimari ayrımlar:

- Domain layer, `UnityEngine` bağımlılığı almaz.
- `RhythmSession`, `HitJudge`, `ScoreTracker`, `BeatEventRuntime` gibi core sınıflar görsel feedback bilmez.
- Python analyzer ve postprocessor beatmap verisini üretmeye devam eder.
- Editor Audio Pipeline penceresi pipeline preview ve JSON atama akışını taşır.
- Sahne feedback'i bu veriyi sadece görsel şiddet olarak kullanır.

Bu ayrım, prototype görselleri değiştirilirken rhythm runtime ve data pipeline'ın stabil kalmasını sağlar.

## 10. Bilinçli sınırlamalar

Bu milestone bilinçli olarak şu özellikleri hedeflemez:

- Final character art.
- Gerçek sprite animasyonları.
- Animator Controller.
- Harici sprite, texture veya material asset.
- Kamera sistemi.
- Gerçek combat state machine.
- Yeni input sistemi.
- Yeni scoring veya judgement kuralı.
- Beatmap schema değişikliği.
- Runtime audio analysis.

Görseller primitive ve değiştirilebilir seviyededir. Bu iyi bir şeydir: Sistem final art'a kilitlenmeden önce ritim sonucunun sahnede nasıl okunacağını test eder.

## 11. Portfolyo değeri

Milestone 4, PulseForge'un portfolyo değerini şu alanlarda güçlendirir:

- Ritim judgement sonucunu sahne üzerinde okunabilir oyun feedback'ine dönüştürme.
- Debug UI ile sahne feedback'ini birlikte çalıştırma.
- Data-driven intensity değerini görsel şiddete bağlama.
- Gameplay kurallarını bozmadan presentation layer geliştirme.
- Prototype seviyesinde ama demo edilebilir bir rhythm-to-combat mapping göstermek.

Demo videosunda bu milestone şu şekilde anlatılabilir:

1. Play Mode'da lane ve OnGUI feedback gösterilir.
2. Guard eventinde parry spark görünür.
3. Strike eventinde enemy üzerinde slash görünür.
4. Miss durumunda player hit taken feedback alır.
5. Düşük/yüksek intensity farkı aynı sistem içinde gösterilir.

Bu, projenin sadece veri üreten bir pipeline değil, o veriyi oyun sahnesinde anlamlı feedback'e dönüştürebilen bir prototype olduğunu gösterir.

## 12. Sonraki adımlar

Bu milestone'dan sonra mantıklı sonraki adımlar:

- Project Cleanup / Demo Recording Prep
  - Demo sahnesi ve dokümanların kayıt hazırlığına getirilmesi.
  - Portfolyo videosu için akışın netleştirilmesi.
  - README ve milestone belgelerinin son kontrolü.
- Rhythm-to-Combat Mapping v2
  - Action pattern ve intensity verisinin daha bilinçli combat sunumuna bağlanması.
  - Event tiplerine göre farklı feedback varyasyonları.
  - Final art'a geçmeden önce daha iyi prototype readability.
- Combat visualization polish
  - Primitive görsellerin daha okunabilir ama hâlâ asset'siz tutulması.
  - Efekt süreleri, renkleri ve kamera framing için manuel ayar iyileştirmeleri.

Sonraki adım seçilirken aynı mimari sınır korunmalıdır: önce davranışı ve veri akışını kanıtla, sonra final görsel kaliteye ilerle.

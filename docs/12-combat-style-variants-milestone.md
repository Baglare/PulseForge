# PulseForge Milestone 5: Combat Style Variants

Bu belge, PulseForge projesinde **Milestone 5: Combat Style Variants** kapsamında eklenen combat-style preset sistemini, Python ve Unity Editor akışlarını, doğrulama yaklaşımını ve portfolyo değerini özetler.

Bu milestone yeni bir runtime gameplay sistemi değildir. Amaç, aynı ritim analizinden farklı oynanabilir dövüş aksiyon dağılımları üretmek ve bu varyantları Unity Editor içinde karşılaştırılabilir hale getirmektir.

## 1. Milestone amacı

Milestone 5'in amacı, tek bir ritim kaynağından farklı dövüş tarzlarına sahip playable beatmap çıktıları üretebilmektir.

Önceki pipeline şu fikri kanıtlıyordu:

```text
WAV veya raw beatmap
→ playable Guard / Strike beatmap
```

Milestone 5 ile aynı ritim verisi şu hale gelir:

```text
WAV veya raw beatmap
→ balanced playable JSON
→ defensive playable JSON
→ aggressive playable JSON
→ bursty playable JSON
```

Böylece geliştirici aynı şarkı veya debug click track üzerinden farklı oyun hissi üreten beatmap'leri yan yana inceleyebilir. Bu, final koreografi üreticisi değil; procedural content generation tarafına atılmış kontrollü ve test edilebilir bir adımdır.

## 2. Bu milestone'da eklenen ana parçalar

| Parça | Sorumluluk |
|---|---|
| `postprocess_beatmap.py` combat-style presetleri | Raw eventleri `legacy`, `balanced`, `defensive`, `aggressive` veya `bursty` aksiyon dağılımına dönüştürür. |
| `run_debug_pipeline.py` combat-style desteği | Tek komutluk pipeline içinde `--combat-style` ve `--burst-window-seconds` argümanlarını taşır. |
| `generate_style_variants.py` | Aynı WAV veya raw beatmap üzerinden birden fazla style variant playable JSON üretir. |
| Unity Audio Pipeline Window style variant generation | Unity Editor içinden Balanced / Defensive / Aggressive / Bursty çıktıları üretmeyi sağlar. |
| Style Variant Comparison paneli | Variant dosyalarını, event sayılarını, Guard / Strike dağılımını, first/last time bilgisini ve dropped count değerini özetler. |

Bu milestone sırasında runtime domain kuralları, sahne feedback sistemi ve Python analyzer detection davranışı bilinçli olarak gereksiz yere değiştirilmedi.

## 3. Combat-style presetleri

### legacy

`legacy`, eski pipeline davranışını korur.

- Hedef his: Önceki Milestone 2 ve 3 akışlarıyla uyumluluk.
- Guard / Strike dağılımı: `--action-mode` ve gerekirse `--pattern` tarafından belirlenir.
- Kullanım senaryosu: Eski pattern tabanlı demo akışını korumak, regression kontrolü yapmak veya manuel action mapping kullanmak.

`legacy` dışındaki presetlerde `--action-mode` ve `--pattern` kullanılmaz. Aksiyon dağılımı combat-style preset tarafından kontrol edilir.

### balanced

`balanced`, Guard ve Strike aksiyonlarını daha dengeli dağıtmayı hedefler.

- Hedef his: Savunma ve saldırı arasında temel debug combat ritmi.
- Guard / Strike dağılımı: Genel olarak iki aksiyonun da okunabilir şekilde görünmesini sağlar.
- Kullanım senaryosu: Varsayılan demo, yeni WAV denemeleri ve genel pipeline kontrolü.

### defensive

`defensive`, Guard ağırlıklı bir oynanış hissi üretir.

- Hedef his: Oyuncunun daha çok savunma, parry ve timing kontrolü yaptığı bir akış.
- Guard / Strike dağılımı: Guard sayısı Strike sayısından belirgin şekilde yüksek olma eğilimindedir.
- Kullanım senaryosu: Parry feedback, Guard timing ve savunma odaklı demo karşılaştırması.

### aggressive

`aggressive`, Strike ağırlıklı bir oynanış hissi üretir.

- Hedef his: Oyuncunun daha çok saldırdığı, slash feedback'in daha sık görüldüğü bir akış.
- Guard / Strike dağılımı: Strike sayısı Guard sayısından belirgin şekilde yüksek olma eğilimindedir.
- Kullanım senaryosu: Slash feedback, saldırı temposu ve aksiyon yoğunluğu göstermek.

### bursty

`bursty`, yoğun veya yakın event kümelerinde daha patlamalı saldırı hissi üretmeyi hedefler.

- Hedef his: Yakın zamanlı veya yüksek yoğunluklu eventlerde daha saldırgan kısa patlamalar.
- Guard / Strike dağılımı: Yakın event pencereleri ve intensity değerleri Strike yönünü güçlendirebilir.
- Kullanım senaryosu: Drop benzeri yoğun anları veya kısa aksiyon patlamalarını debug seviyesinde denemek.

`bursty` için `--burst-window-seconds` değeri kullanılır. Bu değer, yakın eventlerin burst davranışı için hangi zaman penceresinde değerlendirileceğini belirler.

## 4. Python tarafındaki akış

Python tarafında Milestone 5 akışı şu şekilde çalışır:

```text
WAV veya raw beatmap
→ analyzer
→ raw beatmap JSON
→ postprocessor
→ combat-style presetleri
→ playable JSON dosyaları
```

WAV input kullanılırsa analyzer önce raw beatmap üretir. Raw beatmap input kullanılırsa analyzer adımı atlanır ve mevcut raw JSON doğrudan postprocessor'a verilir.

`generate_style_variants.py` varsayılan olarak şu dosyaları üretir:

```text
BM_Playable_<name>_Balanced.json
BM_Playable_<name>_Defensive.json
BM_Playable_<name>_Aggressive.json
BM_Playable_<name>_Bursty.json
```

Her style için postprocess report da üretilebilir:

```text
tools/audio_analyzer/out/<name>_<style>_postprocess_report.json
```

Bu raporlar dropped event sayısı, action count ve combat-style bilgisi gibi debug verileri içerir.

## 5. Unity Editor tarafındaki akış

Unity tarafında kullanım akışı:

1. `Tools > PulseForge > Audio Pipeline` penceresi açılır.
2. `Input Audio Clip` alanına WAV seçilir.
3. Detection mode, difficulty, output name ve output directory kontrol edilir.
4. `Generate Style Variants` çalıştırılır.
5. Balanced / Defensive / Aggressive / Bursty çıktıları pencerede görünür.
6. Comparison panelinden event ve action dağılımları incelenir.
7. `Preview` ile timeline preview hedefi seçilen variant'a çevrilir.
8. Sahnede `DebugRhythmPrototypeController` olan GameObject seçilir.
9. `Assign` ile ilgili variant prototype'a atanır.
10. Play Mode'da seçilen variant oynanır.

Bu akış terminale dönmeden aynı input için farklı playable beatmap çıktıları üretmeyi ve karşılaştırmayı sağlar.

## 6. Style Variant Comparison paneli

Style Variant Comparison paneli her variant için şu bilgileri gösterir:

- `Found / Not found`: Beklenen JSON asset dosyasının bulunup bulunmadığı.
- `event count`: Playable JSON içindeki toplam event sayısı.
- `Guard count`: Guard action sayısı.
- `Strike count`: Strike action sayısı.
- `first / last event time`: İlk ve son event zamanları.
- `dropped count`: Postprocess report varsa düşürülen event sayısı.
- `Ping / Select`: Variant JSON'u Project panelinde bulmayı kolaylaştırır.
- `Preview`: Timeline preview'de playable lane'i seçilen variant JSON ile çizer.
- `Assign`: Seçili `DebugRhythmPrototypeController` üzerindeki `debugBeatMapJson` alanına ilgili variant JSON'u atar.

Parse hatası veya eksik asset durumunda panelin çökmesi beklenmez. İlgili satırda kısa bir hata veya `n/a` bilgisi gösterilir.

## 7. Runtime prototype ile bağlantı

Unity Editor penceresinden bir variant assign edildiğinde, seçilen JSON `DebugRhythmPrototypeController` tarafından yüklenir. Prototype bu JSON'u `DebugBeatMapJsonParser` üzerinden `BeatEventData` listesine dönüştürür.

Runtime tarafında temel aksiyonlar hâlâ iki tanedir:

- `Guard`
- `Strike`

`Guard` eventleri parry feedback'e, `Strike` eventleri slash feedback'e dönüşür. Miss veya timeout durumunda player tarafında hit taken feedback gösterilir.

Milestone 4'ten gelen intensity bağlantısı korunur. JSON içindeki intensity değeri sahne feedback'inde efekt ölçeği, parlaklık ve miss shake şiddeti gibi görsel alanlara yansıyabilir.

## 8. Test ve doğrulama yaklaşımı

Python testleri proje kökünden şu komutla çalıştırılır:

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

Bu testler combat-style presetleri, variant generation, schemaVersion koruması ve Defensive / Aggressive action dağılımı gibi davranışları kapsar.

Unity tarafında manuel doğrulama akışı:

1. `Tools > PulseForge > Audio Pipeline` penceresini aç.
2. WAV seç.
3. `Generate Style Variants` çalıştır.
4. Balanced / Defensive / Aggressive / Bursty satırlarının `Found` olduğunu kontrol et.
5. Defensive çıktıda Guard sayısının yüksek olduğunu kontrol et.
6. Aggressive çıktıda Strike sayısının yüksek olduğunu kontrol et.
7. `Preview` ile timeline'da seçili variant'ı göster.
8. `Assign` ile variant'ı prototype'a ata.
9. Play Mode'da Guard / Strike feedback davranışını test et.

Bu milestone için Unity runtime gameplay kuralı değişmediği için ana risk, EditorWindow akışının doğru dosyaları üretmesi, bulması, özetlemesi ve assign etmesidir.

## 9. Bilinçli sınırlamalar

- Bu hâlâ final koreografi üreticisi değildir.
- Style presetleri basit ve deterministiktir.
- Henüz gerçek müzikal bölüm analizi yoktur.
- Verse, chorus, drop gibi yapısal müzik analizi yoktur.
- Heavy slash, burst action veya guard break gibi ayrı Unity event type'ları henüz yoktur.
- JSON `schemaVersion: 1` hâlâ `Guard` / `Strike` aksiyonlarıyla sınırlıdır.
- Beatmap editor yoktur.
- Variant comparison paneli waveform editor değildir.
- `bursty` gerçek müzik yapısını anlamaz; yakın event ve intensity temelli basit bir debug yaklaşımıdır.

Bu sınırlamalar bilinçlidir. Milestone'un hedefi final tasarım üretmek değil, aynı ritim verisinden farklı oynanabilir combat mapping varyantları üretilebildiğini göstermektir.

## 10. Portfolyo değeri

Milestone 5, PulseForge'un portfolyo değerini şu alanlarda güçlendirir:

- Data pipeline: WAV veya raw beatmap verisinden çoklu playable output üretimi.
- Procedural content generation başlangıcı: Aynı inputtan farklı dövüş tarzları çıkarma.
- Editor tooling: Terminale dönmeden variant üretme, preview etme ve prototype'a atama.
- Variant comparison: Event count ve Guard / Strike dağılımını Unity Editor içinde karşılaştırma.
- Test edilebilir Python araçları: Combat-style davranışlarının unittest ile doğrulanması.
- Mimari ayrım: Runtime/domain tarafını büyütmeden tooling ve data üretim katmanını genişletme.

Bu milestone, PulseForge'u yalnızca ritim prototipi olmaktan çıkarıp veri üretimi, editör aracı ve oynanabilir çıktı karşılaştırması olan daha kapsamlı bir teknik prototipe yaklaştırır.

## 11. Sonraki adımlar

Önerilen sonraki adımlar:

1. Gerçek müzik WAV denemeleri yapmak.
2. Style presetlerini gerçek test çıktıları üzerinden iyileştirmek.
3. Combat event type sistemini genişletmek.
4. `HeavySlash`, `Burst`, `GuardBreak` gibi yeni action metadata fikirlerini değerlendirmek.
5. Gerçek beatmap editor veya waveform preview araştırmak.
6. Demo video veya GIF hazırlığında Defensive / Aggressive farkını özellikle göstermek.
7. Pipeline çıktılarının hangi durumlarda fazla yoğun veya fazla seyrek kaldığını raporlamak.

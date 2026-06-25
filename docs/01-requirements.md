# PulseForge Gereksinim Analizi v0.1

## 1. Ürün tanımı

PulseForge, kullanıcının seçtiği bir müziği önceden analiz eder, müziğin ritmik yapısını görselleştirir ve bu analizden oynanabilir bir 2D dövüş koreografisi üretir.

Temel deneyim:

1. Kullanıcı bir müzik dosyası seçer.
2. PulseForge dosyayı analiz eder.
3. Kullanıcı ritim noktalarını, güçlü vurguları ve yoğunluk bölgelerini görür.
4. Sistem bu veriden bir dövüş haritası üretir.
5. Kullanıcı ritme göre savunma, saldırı ve kısa combo eylemleri gerçekleştirir.
6. Şarkı sonunda performans sonucu gösterilir.

## 2. Karar durumu

### Kesinleşen kararlar

- Proje adı `PulseForge`.
- İlk hedef Windows masaüstü.
- Oyun istemcisi Unity ve C# ile geliştirilecek.
- Müzik analizi ilk sürümde şarkı başlamadan önce yapılacak.
- Müzik analizi ile dövüş haritası üretimi ayrı sorumluluklar olacak.
- Oynanışın ilk sürümünde iki mantıksal eylem bulunacak: `Guard` ve `Strike`.
- Girdi değerlendirmeleri `Perfect`, `Good` ve `Miss` olacak.
- İlk sürüm tek oyunculu ve yerel çalışacak.
- Hesap, sunucu, çevrim içi skor tablosu ve çok oyunculu mod olmayacak.
- Oyuncu şarkı bitmeden oturumdan çıkarılmayacak; kötü performans sonuç ekranında değerlendirilecek.

### Şimdilik varsayım olan kararlar

- Unity hedefi: Unity 6.3 LTS ailesi.
- Görsel yapı: basit 2D çizim veya silüet.
- Ses analiz aracı: yerel Python uygulaması.
- İlk analiz kütüphanesi: librosa.
- Desteklenen ilk dosya türleri: MP3 ve WAV.
- Tek arena, tek oyuncu karakteri ve tek düşman.
- İlk zorluk seviyesi: `Normal`.

### Henüz açık kararlar

- Nihai çizim tarzı.
- Guard ve Strike dışında yön girdisi kullanılıp kullanılmayacağı.
- Analiz ekranında kullanıcının manuel düzeltme yapıp yapamayacağı.
- Nihai skor formülü.
- Şarkı uzunluğu sınırı.
- Nihai zaman pencereleri.
- Harita üretiminde kullanıcıya yoğunluk veya saldırganlık ayarı sunulup sunulmayacağı.

## 3. Fonksiyonel gereksinimler

| Kimlik | Gereksinim | Öncelik |
|---|---|---|
| FR-001 | Kullanıcı yerel bir MP3 veya WAV dosyası seçebilmelidir. | MVP |
| FR-002 | Sistem dosyanın desteklenip desteklenmediğini doğrulamalıdır. | MVP |
| FR-003 | Sistem analiz işleminin durumunu kullanıcıya göstermelidir. | MVP |
| FR-004 | Sistem tahmini tempo, ritim noktaları, vurgu güçleri ve yoğunluk bölgeleri üretmelidir. | MVP |
| FR-005 | Analiz ekranı waveform, ritim işaretleri ve yoğunluk bölümlerini göstermelidir. | MVP |
| FR-006 | Kullanıcı analiz görünümünü müzikle birlikte önizleyebilmelidir. | MVP |
| FR-007 | Sistem analiz verisinden bir dövüş haritası üretmelidir. | MVP |
| FR-008 | Harita en az parry, saldırı ve kısa combo olaylarını desteklemelidir. | MVP |
| FR-009 | Oyuncu girdileri hedef zamana göre Perfect, Good veya Miss olarak değerlendirilmelidir. | MVP |
| FR-010 | Sonuçlar uygun animasyon, ses ve görsel geri bildirim üretmelidir. | MVP |
| FR-011 | Şarkı sonunda skor, isabet dağılımı ve maksimum combo gösterilmelidir. | MVP |
| FR-012 | Aynı analiz ve seed aynı dövüş haritasını üretmelidir. | MVP |
| FR-013 | Kullanıcı genel ses/girdi gecikmesi offset değerini değiştirebilmelidir. | Sonraki sürüm |
| FR-014 | Daha önce analiz edilen parça yeniden analiz edilmeden açılabilmelidir. | Sonraki sürüm |
| FR-015 | Analiz ve beatmap verileri geliştirici tarafından JSON olarak incelenebilmelidir. | MVP |

## 4. Fonksiyonel olmayan gereksinimler

### NFR-001: Senkronizasyon

- Olay zamanları şarkının mutlak zaman çizelgesine göre tutulmalıdır.
- Zaman ölçümü normal kare zamanına bağımlı olmamalıdır.
- Uzun şarkılarda biriken zaman kayması oluşmamalıdır.
- Perfect ve Good pencereleri yapılandırılabilir olmalıdır.

### NFR-002: Determinizm

Aynı `TrackAnalysis`, `DifficultyProfile` ve `seed` verildiğinde aynı `BeatMap` üretilmelidir.

### NFR-003: Hata dayanıklılığı

Aşağıdaki durumlar uygulamayı çökertmemelidir:

- Bozuk veya desteklenmeyen dosya.
- Çok kısa ya da sessiz kayıt.
- Güvenilir ritim bulunamaması.
- Analiz sürecinin başlatılamaması.
- Eksik, eski veya bozuk JSON.
- Üretilen haritada çakışan olaylar.

### NFR-004: Gizlilik

İlk sürümde müzik dosyaları yerel kalmalıdır. Uzak sunucuya yükleme yapılmamalıdır.

### NFR-005: Test edilebilirlik

- Alan mantığı mümkün olduğunca `UnityEngine` bağımlılığı olmadan yazılmalıdır.
- Zaman değerlendirme ve harita üretme davranışları birim testlerle doğrulanmalıdır.
- Test çalıştırılmadıysa çalıştırılmış gibi raporlanmamalıdır.

### NFR-006: Sürümleme

Analiz ve beatmap çıktıları en az şu sürüm alanlarını taşımalıdır:

- `schemaVersion`
- `analyzerVersion`
- `generatorVersion`

## 5. MVP dövüş bölümleri

### Breather

Düşük yoğunluklu, az girdili geçiş bölümü. Hazırlık animasyonları ve pozisyon değişimleri içerir.

### ParryDuel

Düşmanın ritme göre saldırdığı ve oyuncunun `Guard` kullandığı bölüm.

### Offense

Oyuncunun ağırlıklı olarak `Strike` kullandığı, daha yüksek yoğunluklu bölüm.

### Burst

Kısa ve güçlü zirve bölümü. İki ile dört girdilik sekanslar ve belirgin geri bildirim içerir.

## 6. MVP dışında kalanlar

- Gerçek zamanlı canlı müzik analizi.
- Özel bir yapay zekâ modeli eğitme.
- Tam beatmap editörü.
- Çok oyunculu mod.
- Çevrim içi skor tablosu.
- Kullanıcı hesabı.
- Hikâye modu.
- Çok sayıda karakter, silah ve düşman.
- Şarkının verse, chorus veya drop bölümlerini kusursuz anlamsal olarak tanıma.
- Mobil sürüm.

## 7. Başarı ölçütü

MVP başarılı sayılırsa kullanıcı:

1. Bir parça seçebilir.
2. Analizin nasıl oluştuğunu görsel olarak inceleyebilir.
3. Üretilen dövüşü baştan sona oynayabilir.
4. Girdilerinin zamanlama sonucunu anlayabilir.
5. Aynı parçadan yeniden üretilebilir bir koreografi elde edebilir.

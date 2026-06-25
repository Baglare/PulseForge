# PulseForge Geliştirme Yol Haritası v0.1

Her aşama bir öncekinin kabul ölçütleri geçilmeden genişletilmez.

## Aşama 0: Proje ve belge temeli

### Yapılacaklar

- Unity 6.3 LTS tabanlı 2D URP proje oluşturmak.
- Git deposu başlatmak.
- Unity için uygun `.gitignore` eklemek.
- Planlama belgelerini repo köküne eklemek.
- İlk temiz commit'i oluşturmak.

### Bitiş ölçütü

- Proje Unity'de hatasız açılır.
- Git yalnızca kaynak ve proje ayarlarını izler; `Library`, `Temp`, `Logs` gibi üretilen klasörleri izlemez.
- Belgeler repoda bulunur.

## Aşama 1: Sabit haritalı ritim çekirdeği

### 1A. Domain foundation

- `BeatEventData`
- `JudgementWindows`
- `HitResult`
- `HitJudge`
- Edit Mode birim testleri

Bitiş ölçütü: Tüm sınır testleri geçer ve Domain assembly `UnityEngine` referansı içermez.

### 1B. Olay yaşam döngüsü

- `BeatEventRuntime`
- Event durumları
- `BeatEventScheduler`
- Süresi geçen olayların Miss olması
- Edit Mode testleri

Bitiş ölçütü: Sabit zaman girdileriyle olayların etkinleşme ve kaçırılma davranışı deterministik çalışır.

### 1C. Girdi eşleştirme

- `ActiveEventMatcher`
- Guard/Strike eşleştirmesi
- En yakın uygun olay seçimi
- Bir girdinin yalnızca tek olayı tüketmesi

Bitiş ölçütü: Çakışmaya yakın olaylarda seçim kuralları testlerle kanıtlanır.

### 1D. DSP şarkı saati

- `ISongClock`
- `DspSongClock`
- Planlanmış şarkı başlangıcı
- Kullanıcı offset desteğinin temeli

Bitiş ölçütü: Şarkı zamanı kare hızından bağımsız okunur.

### 1E. Minimal oynanabilir sahne

- Sabit WAV.
- Elle hazırlanmış beat event listesi.
- Guard ve Strike için geçici klavye girdileri.
- Basit ekranda Perfect/Good/Miss yazısı.
- Placeholder karakter hareketleri.

Bitiş ölçütü: Baştan sona oynanabilir tek sabit parça bulunur.

## Aşama 2: Veri sözleşmeleri

- `TrackAnalysisData`
- `BeatMapData`
- JSON şeması
- Sürüm alanları
- Örnek fixture dosyaları
- Loader ve validator testleri

Bitiş ölçütü: Geçerli veri yüklenir; bozuk veya eski veri kontrollü hata verir.

## Aşama 3: Python analizcisi

- Ses çözme ve standartlaştırma.
- BPM ve beat çıkarma.
- Beat strength/accent hesaplama.
- Waveform örnekleme.
- Yoğunluk eğrisi.
- Basit bölümleme.
- `analysis.json` üretme.
- Sentetik click-track testleri.

Bitiş ölçütü: Bilinen BPM değerlerindeki test seslerinde kabul edilebilir ritim noktaları çıkarılır.

## Aşama 4: Forge analiz ekranı

- Dosya seçme.
- Analiz durumu.
- Waveform.
- Beat ve accent marker.
- Yoğunluk bölümleri.
- Müzikle hareket eden playhead.

Bitiş ölçütü: Kullanıcı oyuna geçmeden sistemin parçayı nasıl yorumladığını görebilir.

## Aşama 5: Otomatik koreografi üretimi

- `CombatPhasePlanner`
- `CombatPatternGenerator`
- `DifficultyProfile`
- Seed tabanlı üretim.
- `BeatMapValidator`
- Breather, ParryDuel, Offense ve Burst.

Bitiş ölçütü: Aynı analiz ve seed aynı haritayı üretir; harita temel oynanabilirlik kurallarını geçer.

## Aşama 6: Entegrasyon ve önbellek

- Unity'den yerel analiz sürecini başlatma.
- Track package oluşturma.
- Daha önce analiz edilen parçayı bulma.
- Hata ve iptal akışları.

Bitiş ölçütü: Kullanıcı dosya seçmekten oynanışa kadar tek akış içinde ilerler.

## Aşama 7: Sunum ve portfolyo cilası

- Nihai placeholder yerine tutarlı görsel dil.
- Parry, slash ve impact sesleri.
- Kamera ve ekran geri bildirimi.
- Sonuç ekranı.
- Ayarlar ve offset kalibrasyonu.
- README, mimari diyagram, ekran görüntüleri ve demo videosu.

Bitiş ölçütü: Proje yalnızca çalışmaz; nasıl tasarlandığı ve hangi teknik sorunları çözdüğü açıkça gösterilir.

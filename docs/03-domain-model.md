# PulseForge Alan Modeli v0.1

## 1. İlk iterasyonun sınırı

İlk iterasyon yalnızca şu soruya cevap verir:

> Oyuncu belirli bir ritim olayına hedef zamandan ne kadar erken veya geç bastı ve bunun sonucu Perfect, Good ya da Miss mi?

Bu iterasyonda bulunmayacaklar:

- Ses oynatma.
- Unity sahnesi.
- Animasyon.
- Klavye veya gamepad okuma.
- Event scheduler.
- Combo.
- Skor.
- Python.
- JSON.

Bu sınırlama bilinçlidir. Girdi karar mantığını dış ayrıntılardan ayırır.

## 2. İlk sınıflar

### `RhythmAction`

Amaç: Fiziksel tuşlardan bağımsız oyuncu niyetini temsil etmek.

İlk değerler:

- `Guard`
- `Strike`

Not: İlk `HitJudge` görevinde action eşleştirmesi yapılmaz. Action daha sonra aktif olay seçimi için kullanılacaktır.

### `HitGrade`

Amaç: Zamanlama değerlendirmesinin sınırlı sonuç kümesini temsil etmek.

Değerler:

- `Perfect`
- `Good`
- `Miss`

### `BeatEventData`

Amaç: Değişmeyen tek bir hedef ritim olayını temsil etmek.

Önerilen alanlar:

- `string EventId`
- `double TargetTimeSeconds`
- `RhythmAction Action`
- `float Intensity`

Kurallar:

- `EventId` boş olamaz.
- `TargetTimeSeconds` negatif olamaz.
- `Intensity` 0 ile 1 arasında olmalıdır.
- Sınıf `MonoBehaviour` değildir.
- Oluşturulduktan sonra değerleri dışarıdan değiştirilmemelidir.

### `JudgementWindows`

Amaç: Perfect ve Good zaman aralıklarını yapılandırmak.

Önerilen alanlar:

- `double PerfectWindowSeconds`
- `double GoodWindowSeconds`

Kurallar:

- İki değer de sıfırdan büyük olmalıdır.
- Perfect penceresi Good penceresinden büyük olamaz.
- Sınırlar dahildir. Örneğin tam Perfect sınırındaki girdi Perfect sayılır.

### `HitResult`

Amaç: Tek bir değerlendirme sonucunu taşımak.

Önerilen alanlar:

- `string EventId`
- `HitGrade Grade`
- `double TimingErrorSeconds`

`TimingErrorSeconds` işaretli tutulur:

- Negatif: oyuncu erken bastı.
- Pozitif: oyuncu geç bastı.
- Sıfır: tam hedef zamanda bastı.

### `HitJudge`

Amaç: Saf zamanlama kuralını uygulamak.

Önerilen operasyon:

```text
Judge(inputTimeSeconds, beatEvent, judgementWindows) -> HitResult
```

Kural:

```text
error = inputTimeSeconds - beatEvent.TargetTimeSeconds
absoluteError = abs(error)

absoluteError <= perfectWindow  => Perfect
absoluteError <= goodWindow     => Good
aksi                             => Miss
```

`HitJudge`:

- Sistem saatini kendi okumaz.
- Unity API çağırmaz.
- Event listesini yönetmez.
- Skor hesaplamaz.
- Animasyon veya ses başlatmaz.

## 3. Bir sonraki iterasyonda eklenecek sınıflar

İlk iterasyon doğrulandıktan sonra:

### `BeatEventRuntime`

Bir olayın oturum durumunu taşır:

- `Pending`
- `Hit`
- `Missed`

### `BeatEventScheduler`

- Yaklaşan olayları etkinleştirir.
- Süresi geçen olayları Miss olarak kapatır.
- Olay listesinin zaman sırasını yönetir.

### `ActiveEventMatcher`

- Oyuncunun mantıksal eylemine uyan aktif olayları bulur.
- En yakın uygun olayı seçer.
- Tek girdinin yalnızca tek olayı tüketmesini sağlar.

Bu üç sorumluluk ilk görevde `HitJudge` içine gömülmez. Çünkü zamanlama puanı hesaplamak ile olay seçmek aynı problem değildir.

## 4. Tasarım gerekçesi

Bu model `Separation of Concerns` ve `Single Responsibility Principle` uygular.

Kazançları:

- Zamanlama sınırları kolay test edilir.
- Input sistemi değişse bile karar mantığı değişmez.
- DSP saati daha sonra güvenle bağlanır.
- Hatalı bir sonuçta sorunun event seçimi mi, zaman hesabı mı olduğu ayırt edilir.

Bedeli:

- İlk bakışta daha fazla küçük sınıf görünür.
- Küçük proje için gereksiz soyutlama riski vardır.

Bu riski sınırlamak için ilk aşamada interface yazılmayacaktır. `HitJudge` için ikinci bir gerçek implementasyon ihtiyacı doğmadan `IHitJudge` oluşturmak yalnızca dekoratif mimaridir.

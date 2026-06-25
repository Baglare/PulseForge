# PulseForge Çalışma Yöntemi

Her Codex görevi aşağıdaki sırayla yürütülür. Amaç aracı serbest bırakmak değil, denetlenebilir küçük değişiklikler üretmektir.

## 1. Görev öncesi

Her görev için şunları yazarız:

- Hedef: Bu görev sonunda hangi tek yetenek kazanılacak?
- Kapsam: Hangi dosya veya modüllere dokunulabilir?
- Kapsam dışı: Özellikle ne yapılmayacak?
- Kabul ölçütleri: İşin tamamlandığını nasıl kanıtlayacağız?
- Testler: Hangi davranışlar otomatik doğrulanacak?

## 2. Codex'e verilen talimat

Prompt şu parçaları içerir:

1. Proje bağlamı.
2. Tek ve dar hedef.
3. Mevcut mimari kurallar.
4. Oluşturulacak veya değiştirilecek dosyalar.
5. Davranış gereksinimleri.
6. Yasaklar.
7. Beklenen testler.
8. Son rapor biçimi.

## 3. Codex çalışırken beklenen davranış

Codex:

- Önce repository yapısını incelemelidir.
- Gereksiz dosyalara dokunmamalıdır.
- Küçük ve anlaşılır değişiklik yapmalıdır.
- Test eklemelidir.
- Çalıştıramadığı testi çalıştırılmış gibi göstermemelidir.
- Varsayımlarını sonuç raporunda belirtmelidir.

## 4. Çıktı geldikten sonra bizim denetimimiz

Her çıktı beş açıdan incelenir:

### Doğruluk

Davranış kabul ölçütlerini gerçekten karşılıyor mu?

### Mimari uyum

Sınıflar doğru sorumlulukta mı? Domain katmanı Unity ayrıntılarına bağlanmış mı?

### Test kalitesi

Yalnızca mutlu yol mu test edilmiş, yoksa sınırlar ve hatalı girdiler de var mı?

### Gereksiz karmaşıklık

İhtiyaç olmayan interface, factory, event bus, singleton veya manager eklenmiş mi?

### Değişiklik yüzeyi

Görev dışı paket, ayar, sahne veya dosyalar değiştirilmiş mi?

## 5. Her görev sonrası öğretici kayıt

### Ne yaptık?

Eklenen davranışı sade biçimde açıklarız.

### Neden böyle yaptık?

Bu davranışın neden bu sırada ve bu sınırlarla yazıldığını açıklarız.

### Hangi kavramla ilgili?

İlgili yazılım mühendisliği veya bilgisayar bilimi kavramını belirtiriz.

### Avantaj ve dezavantaj

Tasarımın kazancını ve bedelini yazarız.

### Kendim nasıl kontrol ederim?

Unity, test kodu ve diff üzerinde bakılacak noktaları belirtiriz.

## 6. Durma kuralı

Bir görev şu üç koşul olmadan tamamlanmış sayılmaz:

1. Kod derleniyor.
2. İlgili testler geçiyor veya neden çalıştırılamadığı açıkça belirtiliyor.
3. Diff, görev kapsamı dışındaki değişiklikleri içermiyor.

Bu koşullardan biri eksikse yeni özelliğe geçilmez; düzeltme promptu hazırlanır.

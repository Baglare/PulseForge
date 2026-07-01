# PulseForge Demo Recording Guide

Bu belge, PulseForge demosunu video kaydı ve portfolyo sunumu için düzenli bir akışla göstermeye yardımcı olur. Amaç yeni özellik eklemek değil; mevcut debug rhythm-combat demo scene, audio pipeline, Forge Preview ve combat visualization parçalarını anlaşılır sırayla sunmaktır.

## 1. Demo videosunun amacı

Demo videosu şu teknik hikayeyi kısa ve net göstermelidir:

1. Bir WAV dosyası Unity Editor içinden audio pipeline'a verilir.
2. Pipeline raw ve playable beatmap çıktısı üretir.
3. Unity penceresi timeline preview ve report summary ile çıktıyı görünür kılar.
4. Generated playable JSON debug prototype'a atanır.
5. Play Mode'da countdown, lane markerları, Guard/parry, Strike/slash, Miss/hit taken, score, combo ve intensity farkı görülebilir.

Video final oyun kalitesi iddiası taşımamalıdır. Bu kayıt, veri pipeline'ından oynanabilir ritim-combat prototipine uzanan teknik akışı kanıtlamak içindir.

## 2. Demo öncesi hazırlık

Kayıttan önce şu kontrolleri yap:

1. Unity projesini temiz aç.
2. Console penceresinde kırmızı hata olmadığını kontrol et.
3. Demo sahnesinin açılabildiğini doğrula.
4. `Tools > PulseForge > Audio Pipeline` menüsünün göründüğünü kontrol et.
5. Demo WAV dosyasının Project panelinde bulunduğunu kontrol et.
6. Playable JSON atanacak `DebugRhythmPrototypeController` objesini sahnede bul.
7. Kayıt çözünürlüğünü ve Unity layout'unu önceden sabitle.
8. Gereksiz pencereleri kapat; Scene/Game, Inspector, Project ve Audio Pipeline pencereleri yeterlidir.

Temiz bir kayıt için pipeline çıktı klasöründe önceki geçici raporlar kalmışsa bunları commit'e hazırlama. Gerekirse kayıt öncesi değil, kayıt sonrası repository cleanup kontrolünü yap.

## 3. Unity sahnesini açma

Unity içinde demo sahnesini aç:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

Sahnede `Debug Rhythm Prototype` adlı GameObject'i seç. Inspector'da `DebugRhythmPrototypeController` component'inin göründüğünü kontrol et.

Kayıtta bu adım kısa tutulabilir. Ama izleyicinin demo sahnesinin gerçek Unity sahnesi olduğunu görmesi için sahne adı ve controller Inspector'ı bir iki saniye gösterilebilir.

## 4. Audio Pipeline penceresini gösterme

Unity menüsünden pencereyi aç:

```text
Tools > PulseForge > Audio Pipeline
```

Kayıtta pencerenin şu alanlarını göster:

- Input audio seçimi.
- Output name ve pipeline ayarları.
- Detection mode.
- Difficulty ve action mode.
- Timeline preview alanı.
- Report summary alanı.
- Generated JSON seçme ve prototype'a atama butonları.

Bu pencere final beatmap editor değildir. Demo sırasında "pipeline çıktısını incelemek ve prototype'a bağlamak için kullanılan Editor aracı" olarak anlatılmalıdır.

## 5. WAV seçme ve pipeline çalıştırma

Audio Pipeline penceresinde şu akışı izle:

1. `Input Audio Clip` alanına demo WAV dosyasını ata.
2. Varsa `Expected Beat Map JSON` alanına referans beatmap JSON'u ata.
3. Output name, pattern, detection mode, difficulty ve action mode değerlerini kontrol et.
4. `Run Pipeline` butonuna bas.
5. Pipeline tamamlanınca generated raw/playable JSON durumunu kontrol et.

Demo WAV için mevcut proje içi debug audio kullanılabilir:

```text
Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav
```

Kayıtta terminal veya dosya sistemi ayrıntısına girmek zorunda değilsin. Odak, Unity Editor üzerinden pipeline'ın çalıştırılabildiğini göstermektir.

## 6. Timeline preview'i gösterme

Pipeline tamamlandıktan sonra timeline preview alanını göster.

Özellikle şunlara dikkat çek:

- Raw event dağılımı.
- Playable event dağılımı.
- Guard ve Strike aksiyonlarının playable çıktıda ayrılması.
- Timeline üzerinde event yoğunluğunun okunabilmesi.
- Raw verinin doğrudan gameplay olmadığını, playable forma dönüştürüldüğünü gösteren fark.

Bu bölümde uzun açıklama gerekmez. Görsel olarak "pipeline ne üretti?" sorusuna cevap vermesi yeterlidir.

## 7. Pipeline report summary'i gösterme

Reports panelinde oluşan özetleri göster:

- Analysis report: Ses analizi ve tespit edilen aday eventler.
- Postprocess report: Raw eventlerin playable beatmap'e dönüştürülme özeti.
- Compare report: Expected beatmap varsa tolerans ve fark bilgisi.

Bu raporlar demo için önemlidir çünkü sadece JSON üretildiğini değil, pipeline'ın neden o çıktıyı verdiğini de gösterir.

## 8. Generated playable JSON'u prototype'a atama

Pipeline sonucunda generated playable JSON hazır olduğunda:

1. `Ping / Select Generated JSON` ile dosyayı Project panelinde seç.
2. Hierarchy'de `Debug Rhythm Prototype` GameObject'ini seç.
3. `Assign to Selected Debug Prototype` butonuna bas.
4. Inspector'da `Debug Beat Map Json` alanının generated playable JSON ile dolduğunu kontrol et.

Bu adım video için kritiktir. Pipeline çıktısının sadece dosya olarak kalmadığını, doğrudan oynanabilir prototype'a bağlandığını gösterir.

## 9. Play Mode'da ritim-combat demoyu oynatma

Prototype'a JSON atandıktan sonra:

1. Game view'i görünür yap.
2. Play Mode'a gir.
3. `Start / Restart` butonuna bas.
4. Countdown'ın bitmesini bekle.
5. Lane markerları hit line'a yaklaşırken doğru inputları ver.

Kontroller:

```text
Space = Guard
J     = Strike
```

Amaç mükemmel oynanış göstermek değil, sistem davranışını okunabilir şekilde göstermektir. Birkaç doğru hit ve bir kontrollü miss göstermek portfolyo anlatımı için daha faydalıdır.

## 10. Gösterilecek temel anlar

Kayıtta şu anları bilinçli olarak yakala:

- Countdown.
- Lane markerları.
- Guard / parry.
- Strike / slash.
- Miss / hit taken.
- Score / combo.
- Intensity farkı.

Önerilen kısa akış:

1. Countdown başlar.
2. İlk birkaç lane markerı görünür.
3. Bir Guard eventinde `Space` ile parry feedback gösterilir.
4. Bir Strike eventinde `J` ile slash feedback gösterilir.
5. Bir eventi bilinçli kaçırarak `MISS / HIT TAKEN` gösterilir.
6. Score ve combo değişimi kısa süre gösterilir.
7. Farklı intensity değerlerinde efekt ölçeği, parlaklık veya shake farkı gösterilir.

## 11. Video sırasında söylenebilecek kısa açıklamalar

Kısa ve teknik açıklama örnekleri:

- "Bu demo, WAV dosyasından playable beatmap'e giden pipeline'ı Unity içinden gösteriyor."
- "Raw eventler doğrudan gameplay verisi değil; postprocess sonrası Guard ve Strike aksiyonlarına dönüşüyor."
- "Timeline preview, raw ve playable çıktının farkını hızlıca okumak için var."
- "Report summary, analyzer ve postprocessor kararlarını kayıtta görünür kılıyor."
- "Generated playable JSON, seçili debug prototype'a atanıp Play Mode'da oynatılıyor."
- "Sahne feedback'i final art değil; parry, slash ve hit taken sonuçlarını okunabilir kılan debug visualization katmanı."
- "Intensity değeri, aynı ritim eventlerinin sahnede farklı güçte hissedilmesini sağlıyor."

## 12. Bilinçli sınırlamalar

Videoda şu sınırlamaları saklama:

- Bu final oyun değildir.
- Combat visualization prototype seviyesindedir.
- Final animasyon, sprite seti veya karakter sistemi yoktur.
- Audio analyzer gerçek zamanlı runtime analiz yapmaz.
- MP3 import veya dış audio dependency akışı yoktur.
- Timeline preview final beatmap editor değildir.
- Report summary debug ve demo amaçlıdır.
- Generated output dosyaları repository'ye kontrolsüz eklenmemelidir.

Bu sınırlamalar projeyi zayıflatmaz; prototipin hangi problemi çözdüğünü netleştirir.

## 13. Kayıt sonrası kontrol listesi

Kayıttan sonra şunları kontrol et:

- Video countdown'ı gösteriyor mu?
- Audio Pipeline penceresi ve pipeline çalıştırma adımı görünüyor mu?
- Timeline preview okunabiliyor mu?
- Report summary kısa da olsa görünüyor mu?
- Generated playable JSON'un prototype'a atandığı belli mi?
- Play Mode'da Guard/parry görünüyor mu?
- Play Mode'da Strike/slash görünüyor mu?
- Miss/hit taken örneği var mı?
- Score/combo alanı okunuyor mu?
- Intensity farkı en az bir anda seçilebiliyor mu?
- Unity Console'da kayıt sırasında kırmızı hata görünmüyor mu?
- Repository'ye geçici report JSON, debug CSV veya analyzer output eklenmemiş mi?


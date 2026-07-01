# PulseForge Milestone 3: Forge Preview / Beatmap Visualization

Bu belge, PulseForge projesinde **Milestone 3: Forge Preview / Beatmap Visualization** aşamasında tamamlanan işleri, kullanım akışını, mimari sınırları ve portfolyo değerini özetler.

Bu milestone bir runtime gameplay milestone'u değildir. Ana odak Unity Editor içinde audio pipeline çıktılarının daha görünür, incelenebilir ve demo edilebilir hale gelmesidir.

## 1. Milestone amacı

Milestone 3'ün amacı, Milestone 2'de kurulan audio pipeline çıktılarının Unity içinde daha iyi okunmasını sağlamaktır.

Önceki aşamada WAV dosyasından raw beatmap JSON üretilebiliyor, bu veri playable beatmap JSON'a dönüştürülebiliyor ve debug prototype'a atanabiliyordu. Ancak bu akış çoğunlukla dosya isimleri, konsol çıktıları ve manuel kontrol üzerinden takip ediliyordu.

Bu milestone ile pipeline çıktıları Unity Editor penceresinde daha açık hale getirildi:

- Raw beatmap ile playable beatmap arasındaki fark görünür oldu.
- Analysis, postprocess ve compare raporları okunabilir özetlere dönüştü.
- Generated playable JSON'u bulma, seçme ve prototype'a atama akışı hızlandı.
- Pipeline sonucu demo videosunda gösterilebilir bir görsel forma kavuştu.

## 2. Bu milestone'da eklenen ana parçalar

Bu milestone'da odak Unity Editor tooling tarafındadır. Runtime, domain ve Python davranışları bilinçli olarak değiştirilmedi.

Eklenen ana parçalar:

- `PulseForgeAudioPipelineWindow.cs`
  - Audio Pipeline penceresinin Forge Preview iş akışını taşıyan ana EditorWindow sınıfı.
- `BeatmapTimelinePreviewDrawer.cs`
  - Raw ve playable beatmap eventlerini timeline üzerinde gösteren drawer.
- `PipelineReportSummaryDrawer.cs`
  - Analysis, postprocess ve compare raporlarını Unity içinde özetleyen drawer.

Bu parçalar birlikte, pipeline'ın sadece "çalıştı veya çalışmadı" seviyesinde değil, "ne üretti ve neden farklı üretti" seviyesinde incelenmesini sağlar.

## 3. Unity Audio Pipeline penceresi

Unity penceresi şu menüden açılır:

```text
Tools > PulseForge > Audio Pipeline
```

Pencere, geliştiricinin tek yerden şu işlemleri yapmasına izin verir:

- WAV `AudioClip` seçme.
- Expected beatmap JSON seçme.
- Output name, pattern, detection mode, difficulty ve action mode ayarlama.
- Python pipeline'ı çalıştırma.
- Üretilen raw/playable JSON durumunu görme.
- Timeline preview üzerinden event dağılımını inceleme.
- Pipeline raporlarını panel içinde okuma.
- Generated playable JSON'u Project panelinde ping/select etme.
- Generated playable JSON'u seçili `DebugRhythmPrototypeController` component'ine atama.

Bu pencere final beatmap editor değildir. Event sürükleme, event silme/ekleme, waveform üzerinde nokta düzeltme veya authoring aracı gibi davranmaz. Şu anki rolü, pipeline sonucunu görünür kılan bir Editor preview ve bağlantı aracıdır.

## 4. Beatmap Timeline Preview

`BeatmapTimelinePreviewDrawer`, pipeline'ın ürettiği raw ve playable eventleri timeline üzerinde gösterir.

Raw beatmap, analyzer'ın WAV dosyasından çıkardığı ilk ritim adaylarını temsil eder. Bu veri daha doğrudan, daha ham ve analyzer kararlarına daha yakındır.

Playable beatmap ise postprocessor'dan geçmiş, difficulty ve action mapping kurallarına göre oyun prototipinde kullanılmaya daha uygun hale getirilmiş veridir.

Timeline preview şu farkları görünür yapar:

- Analyzer'ın kaç event bulduğu.
- Postprocessor'ın hangi eventleri koruduğu veya seyrelttiği.
- Eventlerin zaman ekseninde nasıl dağıldığı.
- Raw veri ile playable veri arasındaki yoğunluk farkı.
- Guard/Strike gibi aksiyonların playable çıktıda nasıl temsil edildiği.

Bu görünüm, final waveform editor değildir. Ses dalga formu düzenleme veya sample-level analiz amacı taşımaz. Ama pipeline'ın ritim eventlerini nasıl dönüştürdüğünü hızlıca kontrol etmek için yeterli bir görsel katman sağlar.

## 5. Pipeline Reports paneli

`PipelineReportSummaryDrawer`, pipeline'ın ürettiği raporları Unity içinde okunabilir özetlere dönüştürür.

Panelin amacı JSON raporlarını ham dosya olarak açma ihtiyacını azaltmaktır. Geliştirici, Audio Pipeline penceresinden şu rapor tiplerini hızlıca inceleyebilir:

- Analysis report
  - Analyzer'ın WAV dosyasından çıkardığı event sayısı, detection mode ve temel analiz sonucunu özetler.
- Postprocess report
  - Raw eventlerin playable beatmap'e dönüştürülürken nasıl filtrelendiğini veya eşlendiğini gösterir.
- Compare report
  - Expected beatmap ile generated playable beatmap arasındaki timing farklarını, tolerans durumunu ve offset bilgisini özetler.

Bu raporlar özellikle demo ve debugging için önemlidir. Çünkü sadece "JSON üretildi" demek yerine, pipeline'ın neden belirli bir çıktı verdiğini açıklamaya yardımcı olur.

## 6. Generated JSON seçme ve prototype'a atama akışı

Pipeline çalıştıktan sonra generated playable JSON dosyası Unity asset olarak bulunabilir hale gelir.

Audio Pipeline penceresi bu dosya için iki pratik akış sunar:

1. `Ping / Select Generated JSON`
   - Generated playable JSON Project panelinde seçilir.
   - Dosyanın gerçekten üretildiği ve Unity tarafından import edildiği hızlıca kontrol edilir.

2. `Assign to Selected Debug Prototype`
   - Sahnede `DebugRhythmPrototypeController` bulunan GameObject seçiliyken çalışır.
   - Generated playable JSON, controller üzerindeki debug beatmap JSON alanına atanır.
   - Böylece pipeline çıktısı debug rhythm-combat prototype tarafından kullanılabilir.

Bu işlem sahneyi otomatik kaydetmek zorunda değildir. Atama sonrası sahne değişikliği kullanıcı tarafından bilinçli şekilde kaydedilmelidir.

## 7. Kullanım adımları

1. Unity'de demo sahnesini aç:

```text
Assets/PulseForge/Demo/Scenes/PF_DebugRhythmPrototype.unity
```

2. Audio Pipeline penceresini aç:

```text
Tools > PulseForge > Audio Pipeline
```

3. `Input Audio Clip` alanına WAV tabanlı demo audio clip'i ata.

4. İsteğe bağlı olarak `Expected Beat Map JSON` alanına referans beatmap JSON'u ata.

5. Output name, pattern, detection mode, difficulty ve action mode ayarlarını kontrol et.

6. `Use Expected Compare` seçeneğini, compare report isteniyorsa açık tut.

7. `Run Pipeline` butonuna bas.

8. Pipeline tamamlandıktan sonra generated raw/playable JSON durumunu kontrol et.

9. Timeline preview içinde raw ve playable eventlerin zaman eksenindeki dağılımını incele.

10. Reports panelinde analysis, postprocess ve compare özetlerini oku.

11. `Ping / Select Generated JSON` ile playable JSON'u Project panelinde seç.

12. Sahnede `DebugRhythmPrototypeController` bulunan GameObject'i seç.

13. `Assign to Selected Debug Prototype` ile generated playable JSON'u prototype'a ata.

14. Play Mode'a girip debug prototype'ın atanan JSON ile çalıştığını kontrol et.

## 8. Runtime / domain / Python ayrımı

Bu milestone'da bilinçli olarak şu katmanlar değiştirilmedi:

- Runtime gameplay davranışı.
- Domain rhythm judgement kuralları.
- Beat event matching davranışı.
- Score/combo sistemi.
- Python analyzer algoritması.
- Python postprocessor algoritması.
- Python compare tool davranışı.

Milestone 3, bu sistemlerin ürettiği ve tükettiği veriyi Unity Editor içinde daha görünür kılar. Yani gameplay kuralı eklemek yerine tooling kalitesini artırır.

Bu ayrım önemli çünkü pipeline visualization geliştirilirken runtime davranışının beklenmedik şekilde değişmemesi gerekir. Editor tooling ayrı kalırsa, beatmap üretim ve inceleme deneyimi iyileştirilebilirken domain ve runtime sistemleri stabil kalır.

## 9. Test ve doğrulama yaklaşımı

Bu milestone için doğrulama ağırlıklı olarak Editor smoke test ve manuel görsel kontrol üzerinden yapılır.

Kontrol edilmesi gereken ana noktalar:

- Audio Pipeline penceresi hatasız açılıyor mu?
- WAV seçilip pipeline çalıştırılabiliyor mu?
- Raw beatmap JSON ve playable beatmap JSON üretimi pencere içinde görülebiliyor mu?
- Timeline preview raw ve playable eventleri gösteriyor mu?
- Analysis, postprocess ve compare raporları okunabilir özetlere dönüşüyor mu?
- Generated playable JSON Project panelinde ping/select edilebiliyor mu?
- Generated playable JSON seçili `DebugRhythmPrototypeController` component'ine atanabiliyor mu?
- Atanan JSON Play Mode'da debug prototype tarafından kullanılabiliyor mu?

Bu milestone'da yeni runtime/domain test davranışı beklenmez, çünkü runtime ve domain kodu değiştirilmemiştir. Mevcut Unity Edit Mode testleri ve Python unittest'leri Milestone 1 ve Milestone 2 davranışlarını korumak için önemini sürdürür.

## 10. Bilinçli sınırlamalar

Bu milestone şu özellikleri amaçlamaz:

- Final beatmap editor.
- Waveform editor.
- Event sürükleme, silme veya ekleme.
- Audio waveform üzerinde sample-level düzeltme.
- Runtime gameplay değişikliği.
- Yeni combat sistemi.
- Yeni scoring sistemi.
- Yeni Python analyzer algoritması.
- Generated JSON veya report dosyalarını kalıcı içerik olarak repoya ekleme.

Timeline preview ve report summary panelleri debug ve demo amaçlıdır. Amaç, pipeline çıktısını anlaşılır kılmak ve bir sonraki geliştirme kararlarını daha görünür veriye dayandırmaktır.

## 11. Portfolyo değeri

Bu milestone'un portfolyo değeri, sadece oyun sahnesi göstermekten farklıdır. Adayın veya geliştiricinin şu yetkinlikleri gösterebilmesini sağlar:

- Unity Editor tooling geliştirme.
- Data pipeline çıktısını görselleştirme.
- Raw veri ile oyun için işlenmiş veri arasındaki farkı anlatma.
- JSON raporlarını kullanıcıya okunabilir özet olarak sunma.
- Python tooling ile Unity Editor arasındaki bağlantıyı yönetme.
- Runtime kodunu değiştirmeden geliştirme deneyimini iyileştirme.
- Demo edilebilir, adım adım açıklanabilir teknik workflow kurma.

Demo videosunda iyi bir akış şu şekilde gösterilebilir:

1. Audio Pipeline penceresini aç.
2. WAV dosyasını seç.
3. Pipeline'ı çalıştır.
4. Raw ve playable timeline görünümlerini göster.
5. Reports panelinde analysis, postprocess ve compare özetlerini göster.
6. Generated playable JSON'u Project panelinde seç.
7. JSON'u seçili debug prototype'a ata.
8. Play Mode'da prototype'ın bu generated JSON ile çalıştığını göster.

Bu akış, PulseForge'un sadece runtime prototip değil, veri üretimi, editör aracı ve görsel doğrulama tarafı olan bir sistem olduğunu somut şekilde anlatır.

## 12. Sonraki adımlar

Bu milestone'dan sonra önerilen sonraki ana milestone:

### Combat Visualization Prototype

Bu aşamada runtime combat feedback'in OnGUI debug görünümünden daha görsel bir 2D prototipe taşınması değerlendirilebilir.

Olası sonraki işler:

- Guard/parry ve strike/slash için basit 2D feedback.
- Lane ile combat feedback arasında daha güçlü görsel bağlantı.
- Hit, miss ve timing feedback için daha okunabilir efektler.
- Mevcut domain ve beatmap pipeline davranışını değiştirmeden runtime sunumu iyileştirme.

Forge Preview tarafında sonraki küçük iyileştirmeler ise ayrı tutulmalıdır:

- Timeline zoom veya ölçek kontrolü.
- Daha ayrıntılı compare görselleştirmesi.
- Report panelinde uyarı seviyeleri.
- Waveform görünümü araştırması.
- Final beatmap authoring/editing aracı için ayrı tasarım çalışması.

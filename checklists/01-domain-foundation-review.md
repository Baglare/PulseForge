# Codex Çıktı Kontrol Listesi 01

Codex görevini tamamladıktan sonra diff ve Unity sonucu bu sırayla kontrol edilir.

## A. Kapsam kontrolü

- [ ] Yalnızca `Runtime/Domain/Rhythm` ve `Tests/EditMode` altında gerekli dosyalar değişti.
- [ ] `Packages/manifest.json` değişmedi.
- [ ] `ProjectSettings` değişmedi.
- [ ] Sahne, prefab, görsel veya ses dosyası eklenmedi.
- [ ] Görev dışı gelecekteki özellikler yazılmadı.

## B. Mimari kontrol

- [ ] Production domain assembly `UnityEngine` referansı taşımıyor.
- [ ] Hiçbir domain tipi `MonoBehaviour` veya `ScriptableObject` değil.
- [ ] `HitJudge` sistem saatini veya Unity zamanını kendi okumuyor.
- [ ] `HitJudge` yalnızca zamanlama sınıflandırması yapıyor.
- [ ] Gereksiz `IHitJudge`, factory, singleton, event bus veya manager yok.

## C. Veri doğrulama kontrolü

- [ ] Boş `EventId` reddediliyor.
- [ ] Negatif veya sonlu olmayan hedef zaman reddediliyor.
- [ ] 0–1 dışındaki veya sonlu olmayan intensity reddediliyor.
- [ ] Sıfır/negatif/sonlu olmayan judgement pencereleri reddediliyor.
- [ ] Perfect penceresi Good penceresinden büyük olamıyor.
- [ ] Sonlu olmayan input zamanı reddediliyor.

## D. Davranış kontrolü

- [ ] Hedef zaman tam isabet Perfect.
- [ ] Perfect sınırları dahil.
- [ ] Perfect dışı fakat Good içi Good.
- [ ] Good sınırları dahil.
- [ ] Good dışı Miss.
- [ ] Erken giriş negatif hata üretiyor.
- [ ] Geç giriş pozitif hata üretiyor.

## E. Test kontrolü

- [ ] Edit Mode test assembly doğru oluşturulmuş.
- [ ] Testler sınır değerlerini kapsıyor.
- [ ] Testler sahne veya frame timing'e bağlı değil.
- [ ] Unity Test Runner'da tüm ilgili testler geçiyor.
- [ ] Codex çalıştırmadığı testleri geçmiş gibi raporlamadı.

## F. Manuel Unity doğrulaması

1. Unity projeyi aç.
2. Console'da derleme hatası olmadığını doğrula.
3. `Window > General > Test Runner` ekranını aç.
4. `EditMode` sekmesini seç.
5. PulseForge domain testlerini çalıştır.
6. Sonucun ekran görüntüsünü veya hata metnini sakla.

## Sonuç kararı

- Bütün kutular geçtiyse: Görev 01 kabul edilir ve commit atılır.
- Davranış hatası varsa: Düzeltme promptu hazırlanır.
- Gereksiz mimari eklendiyse: Refactor promptu hazırlanır.
- Kapsam dışı dosya değiştiyse: O değişiklikler geri alınır ve görev yeniden daraltılır.

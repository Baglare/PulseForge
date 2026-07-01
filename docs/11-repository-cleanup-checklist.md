# PulseForge Repository Cleanup Checklist

Bu belge, demo kaydı veya portfolyo hazırlığı sonrası repository'yi temiz tutmak için kullanılacak kontrol listesidir. Amaç yeni özellik eklemek değil; commit'e girecek belgeleri, kaynak dosyalarını ve geçici çıktıları ayırmaktır.

## 1. Commit öncesi genel kontrol

Commit atmadan önce şu sırayı izle:

1. Değişiklik listesini kontrol et.
2. Sadece bu görevle ilgili dosyaların değiştiğini doğrula.
3. Unity tarafından üretilen geçici klasörlerin listede olmadığını kontrol et.
4. Python analyzer geçici çıktılarının listede olmadığını kontrol et.
5. Scene, prefab, asset, package veya ProjectSettings değişikliği beklenmiyorsa bunları commit'e alma.
6. README ve docs değişikliklerinin Türkçe, net ve uygulanabilir olduğunu kontrol et.
7. Çalıştırdığın testleri ve çalıştıramadığın testleri not et.

## 2. Commit'e girmesi gereken dosyalar

Bu dokümantasyon görevi için commit'e girmesi beklenen dosyalar:

- `README.md`
- `docs/10-demo-recording-guide.md`
- `docs/11-repository-cleanup-checklist.md`
- `.gitignore` yalnızca eksik geçici dosya kuralları eklendiyse

Başka bir görev yapılmadıysa runtime, editor, Python script, scene, prefab veya asset dosyaları bu commit'e girmemelidir.

## 3. Commit'e girmemesi gereken dosyalar

Şu dosyalar ve klasörler commit'e alınmamalıdır:

- `Library/`
- `Temp/`
- `obj/`
- `.codex-compile-temp/`
- `tools/audio_analyzer/out/`
- `__pycache__/`
- `*.pyc`
- `*.pyo`
- geçici report JSON dosyaları.
- geçici debug CSV dosyaları.
- Unity log dosyaları.
- Yerel kullanıcı ayarları.
- Kayıt sırasında oluşan video veya screen capture dosyaları.

Bu dosyalar geliştirme ve demo sırasında oluşabilir, ancak kaynak kodu veya kalıcı proje dokümantasyonu değildir.

## 4. Unity özel kontrol listesi

Unity tarafında şunları kontrol et:

1. `Assets/` altında beklenmeyen `.unity`, `.prefab`, `.asset`, `.mat`, `.png`, `.wav` değişikliği yok.
2. `ProjectSettings/` altında beklenmeyen değişiklik yok.
3. `Packages/manifest.json` veya `Packages/packages-lock.json` değişmedi.
4. `Library/`, `Temp/`, `Logs/` ve `UserSettings/` commit listesinde yok.
5. Unity Console'da demo öncesi veya sonrası kırmızı hata varsa bunu not et.
6. Bu görev dokümantasyon görevi ise Play Mode davranışı değiştirilmedi.

## 5. Python araçları özel kontrol listesi

Python audio analyzer tarafında şunları kontrol et:

1. `tools/audio_analyzer/*.py` dosyalarında beklenmeyen değişiklik yok.
2. `tools/audio_analyzer/tests/` altında beklenmeyen değişiklik yok.
3. `tools/audio_analyzer/out/` commit listesinde yok.
4. `__pycache__/`, `*.pyc` ve `*.pyo` dosyaları commit listesinde yok.
5. Geçici diagnostics CSV veya report JSON dosyaları commit'e eklenmedi.

## 6. Generated output politikası

Generated output dosyaları varsayılan olarak commit'e alınmamalıdır.

Commit'e alınmaması gereken örnekler:

- Analyzer report JSON dosyaları.
- Postprocess report JSON dosyaları.
- Compare report JSON dosyaları.
- Debug frame CSV dosyaları.
- Geçici raw/playable beatmap denemeleri.
- Pipeline çalışma klasörü çıktıları.

Kalıcı bir demo beatmap dosyası commit'e alınacaksa bunun ayrı bir görev olarak açıkça istenmesi gerekir. Dosyanın adı, amacı ve hangi demo akışında kullanılacağı README veya ilgili docs içinde açıklanmalıdır.

## 7. Demo asset politikası

Demo assetleri yalnızca bilinçli seçilmiş ve tekrar kullanılacaksa repository'de kalmalıdır.

Kontrol et:

1. Kayıt sırasında oluşan yeni WAV, video, screenshot veya capture dosyaları commit'e eklenmedi.
2. Var olan demo audio veya beatmap dosyaları yanlışlıkla değiştirilmedi.
3. Yeni demo asset gerekiyorsa bu dokümantasyon cleanup görevinin dışında tutuldu.
4. Generated playable JSON dosyası demo için kalıcı hale getirilecekse ayrı onay ve açıklama var.

## 8. Test komutları

Python testlerini proje kökünden çalıştır:

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

Unity Edit Mode testlerini Unity Test Runner içinden çalıştır:

```text
Window > General > Test Runner > EditMode > Run All
```

Bu ortamda Unity Test Runner çalıştırılamadıysa bunu açıkça not et. Test geçmiyorsa bu dokümantasyon görevi içinde zorla düzeltme yapma; hatayı ve komutu raporla.

## 9. GitHub Desktop ile kontrol adımları

GitHub Desktop kullanıyorsan:

1. Changes sekmesini aç.
2. Dosya listesini bu checklist ile karşılaştır.
3. Sadece beklenen dokümantasyon ve ignore dosyalarının seçili olduğundan emin ol.
4. `Library/`, `Temp/`, `obj/`, `.codex-compile-temp/`, `tools/audio_analyzer/out/`, `__pycache__/`, `*.pyc` veya `*.pyo` görünüyorsa commit'e alma.
5. README ve docs diff'ini oku; metnin görev kapsamı dışında özellik sözü vermediğini kontrol et.
6. Commit mesajını dar kapsamlı yaz.

Örnek commit mesajı:

```text
docs: add demo recording and cleanup guides
```

## 10. Sık yapılan hatalar

Sık görülen hatalar:

- Generated report JSON dosyalarını yanlışlıkla commit'e eklemek.
- Debug CSV dosyalarını kaynak veri sanmak.
- Unity `Library/` veya `Temp/` klasörlerini seçili bırakmak.
- Demo kaydı için oluşan video dosyasını repository'ye eklemek.
- Dokümantasyon görevi sırasında scene veya prefab değişikliğini fark etmemek.
- README'ye ayrıntılı kılavuz gömmek yerine docs dosyalarına yönlendirmeyi unutmak.
- Test çalışmadığı halde geçmiş gibi raporlamak.
- Prototype sınırlamalarını saklayıp final oyun iddiası gibi anlatmak.

# PulseForge — Buradan Başla

Bu dosya ilk oturumda yapılacak işlemleri sırayla verir.

## Adım 1 — Unity projesini oluştur

1. Unity Hub'ı aç.
2. Unity 6.3 LTS ailesinin güncel yamasını kur.
3. Yeni proje oluştururken bir 2D URP şablonu seç.
4. Proje adı: `PulseForge`.
5. Önerilen klasör: `C:\Projects\PulseForge`.
6. Projeyi aç ve ilk import işleminin bitmesini bekle.
7. Console penceresinde kırmızı derleme hatası olmadığını doğrula.
8. Sahneyi şimdilik düzenleme. İlk görev sahne üretmeyecek.

Neden: Aynı motor ailesini sabitlemek, Codex'in dosya ve API varsayımlarını sınırlar. URP seçimi ileride 2D ışık ve ekran efektleri için alan bırakır; ilk domain kodunu etkilemez.

## Adım 2 — Planlama paketini proje köküne yerleştir

Bu paketin içindeki şu öğeleri Unity proje köküne kopyala:

- `.gitignore`
- `AGENTS.md`
- `README.md`
- `docs/`
- `prompts/`
- `checklists/`

Doğru kök görünümü:

```text
PulseForge/
├── Assets/
├── Packages/
├── ProjectSettings/
├── docs/
├── prompts/
├── checklists/
├── AGENTS.md
├── README.md
└── .gitignore
```

Belgeleri `Assets` klasörüne koyma. Kod değiller ve Unity'nin bunları asset olarak işlemesine gerek yok.

## Adım 3 — Git deposunu başlat

Proje kökünde PowerShell veya terminal aç:

```powershell
git init
git add .
git status
git commit -m "chore: initialize PulseForge project and planning docs"
```

`git status` çıktısında `Library`, `Temp`, `Logs`, `Obj` veya `UserSettings` klasörleri görünmemelidir.

Neden: İlk temiz commit geri dönüş noktasıdır. Codex görevinden sonra yalnızca onun yaptığı değişiklikleri diff üzerinden ayırabiliriz.

## Adım 4 — Repository'yi Codex'e aç

Codex'in repository kökünü gördüğünden emin ol. Göreve başlamadan önce `AGENTS.md` ile `docs/` belgelerini okuyabilmesi gerekir.

İlk görevde Codex'e yalnızca şu promptu ver:

- `prompts/01-domain-foundation.md`

Promptu kısaltma veya yanına “bir de sahne kur” gibi ek istek iliştirme. İlk görev yalnızca ritim karar çekirdeği ve Edit Mode testleridir.

## Adım 5 — Codex çıktısını hemen kabul etme

Codex tamamladığında:

1. Değişen dosyaların listesini oku.
2. Repository diff'ini aç.
3. `checklists/01-domain-foundation-review.md` dosyasını sırayla uygula.
4. Unity'yi aç.
5. Derleme hatalarını kontrol et.
6. `Window > General > Test Runner` ekranından Edit Mode testlerini çalıştır.
7. Test sonucu geçmeden commit atma.

## Adım 6 — Sonucu kaydet

Bütün kontroller geçerse:

```powershell
git add Assets/PulseForge
git commit -m "feat: add tested rhythm judgement domain foundation"
```

Kontroller geçmezse yeni özelliğe geçme. Codex'in sonuç raporunu, diff özetini ve Unity hata/test çıktısını değerlendirmek üzere sakla. Sonraki işlem bir düzeltme veya refactor promptu olur.

## Bu adımda ne kazanmış olacağız?

Henüz oyun görünmeyecek. Bunun yerine şu temel garanti kurulacak:

- Bir ritim olayının hedef zamanı var.
- Oyuncunun erken veya geç bastığı ölçülebiliyor.
- Sonuç sınır değerlerinde tutarlı biçimde Perfect, Good veya Miss oluyor.
- Bu davranış sahneye, animasyona ve kare hızına bağlı değil.
- Davranış otomatik testlerle korunuyor.

Bu temel geçmeden müzik senkronizasyonuna geçmek, görünür bir demo uğruna görünmez hataları çoğaltmak olur. İnsanlar buna genellikle “hızlı ilerledik” diyor.

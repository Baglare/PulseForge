# Runtime Custom Song Import ve Sahne Audio Pipeline

Bu özellik, Windows tester build'inde kullanıcının kendi ses dosyasını seçip runtime
Audio Pipeline ayarlarıyla oynanabilir hızlı bir PulseForge oturumuna dönüştürmesini
sağlar. Şarkı hiçbir zaman açılışta veya pipeline tamamlandığında kendiliğinden başlamaz.

Bu akış mevcut Editor Python pipeline'ının yerini almaz. Runtime tarafı hızlı ve
kurulumsuz tester deneyimi için daha küçük bir onset analizi kullanır. Editör içindeki
Python pipeline; difficulty, combat style, rapor ve ayrıntılı postprocess gerektiğinde
esas authoring aracı olmaya devam eder.

## Desteklenen akış

```text
Windows dosya seçici
  -> FFmpeg dönüşümü
  -> 44.1 kHz / stereo / 16-bit PCM WAV
  -> Unity AudioClip yükleme
  -> seçilen amplitude/onset analizi
  -> difficulty min-gap filtresi
  -> combat-style Guard / Strike dağılımı
  -> hazır ve durdurulmuş session
  -> Start / Restart ile DSP audio clock
```

Sahne paneline taşınan Audio Pipeline ayarları:

- `Detection`: `Onset` veya `Amplitude`.
- `Difficulty`: `Easy`, `Normal` veya `Hard`.
- `Combat Style`: `Legacy`, `Balanced`, `Defensive`, `Aggressive` veya `Bursty`.

Bu değerlerin düğmelerine her basış bir sonraki seçeneğe geçer. Ayarlar, ses dosyası
seçilip pipeline çalıştırıldığı anda snapshot olarak uygulanır.

Desteklenen uzantılar:

- `.wav`
- `.mp3`
- `.m4a`
- `.aac`
- `.flac`
- `.ogg`
- `.opus`
- `.wma`
- `.aif`
- `.aiff`

Runtime içe aktarma sınırları:

- En fazla 256 MB kaynak dosya.
- En fazla 15 dakika dönüştürülmüş ses.
- En fazla 512 runtime event; daha fazla peak bulunursa bütün şarkı süresini koruyacak
  biçimde eşit aralıklı örneklenir.
- Dosya seçimi şu anda yalnızca Windows Editor ve Windows standalone build'de vardır.

## Geliştirici: FFmpeg'i build için hazırlama

FFmpeg binary'si boyut ve lisans yönetimi nedeniyle Git'e eklenmez. Build almadan önce
yerel olarak `StreamingAssets` içine hazırlanmalıdır.

1. PowerShell'i repository kökünde aç.
2. Şu komutu çalıştır:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\runtime_audio\setup_ffmpeg.ps1
   ```

3. Script önce PATH üzerinde kurulu bir `ffmpeg.exe` arar.
4. Kurulu FFmpeg yoksa FFmpeg'in resmi indirme sayfasında Windows build sağlayıcısı
   olarak listelenen gyan.dev release essentials ZIP'ini indirir.
5. İndirilen arşivin SHA256 değerini doğrular.
6. Son dosyanın şu konumda olduğunu kontrol et:

   ```text
   Assets/StreamingAssets/PulseForge/ffmpeg.exe
   ```

7. Dosyayı yenilemek istersen komuta `-Force` ekle.

Belirli bir yerel binary kullanmak için:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\runtime_audio\setup_ffmpeg.ps1 `
  -SourceExecutable "C:\Tools\ffmpeg\bin\ffmpeg.exe" `
  -Force
```

## Geliştirici: Windows tester build alma

1. Unity'yi aç ve script compilation tamamlanana kadar bekle.
2. Console'da kırmızı compile error olmadığını doğrula.
3. `File > Build Profiles` penceresini aç.
4. Windows platformunu ve `x86_64` mimarisini seç.
5. Mevcut build scene'i olan `Assets/Scenes/SampleScene.unity` sahnesinin listede etkin
   olduğunu doğrula. Bu sahnede `DebugRhythmPrototypeController` zaten bulunur; yeni
   GameObject veya Inspector bağlantısı eklemek gerekmez.
6. `Build` ile Windows çıktısını oluştur.
7. Build klasöründe aşağıdaki dosyanın bulunduğunu doğrula:

   ```text
   <BuildAdı>_Data/StreamingAssets/PulseForge/ffmpeg.exe
   ```

8. Tester'a yalnızca `.exe` dosyasını değil, Unity'nin oluşturduğu tüm build klasörünü
   tek paket halinde gönder.

## Tester: kendi şarkını açma

1. PulseForge build'ini çalıştır.
2. Sağ panelde `Runtime Audio Pipeline` bölümünü bul.
3. `Detection`, `Difficulty` ve `Combat Style` düğmeleriyle istediğin ayarları seç.
4. `Choose Audio & Run Pipeline` düğmesine bas.
5. Desteklenen bir ses dosyası seç.
6. Arayüzde sırasıyla şu durumların ilerlemesini bekle:

   ```text
   Converting to PCM WAV...
   Loading converted WAV...
   Creating a quick runtime beatmap...
   Ready: <şarkı adı> (<event sayısı> beats)
   ```

7. `Ready` mesajından sonra `Start / Restart` düğmesine bas.
8. Countdown tamamlanınca `Space` ile Guard, `J` ile Strike kullan.
9. Durdurmak için aynı satırdaki `Pause`, devam etmek için `Resume` kullan.

Dönüştürülen geçici WAV kullanıcının `Application.persistentDataPath/ImportedAudio`
klasöründe tutulur ve bir sonraki başarılı içe aktarmada yenilenir.

## Fallback davranışı

- Kullanıcı dosya seçmez veya Windows dialogunu iptal ederse mevcut oturum değişmez.
- Dönüşüm/yükleme/analiz başarısız olursa hata `Runtime Audio Pipeline` ve `Last Feedback`
  alanlarında gösterilir; mevcut Inspector AudioClip ve beatmap korunur.
- Runtime şarkı yalnızca başarılı içe aktarmadan sonra mevcut kaynakların önüne geçer.
- Uygulama yeniden açıldığında Inspector'daki normal AudioClip/beatmap akışı kullanılır.

## Sorun giderme

### `FFmpeg was not found`

1. `tools/runtime_audio/setup_ffmpeg.ps1` scriptini çalıştır.
2. `Assets/StreamingAssets/PulseForge/ffmpeg.exe` dosyasını kontrol et.
3. Yeniden build al; eski build klasörüne yalnızca `.exe` kopyalamak yeterli değildir.

### `Unsupported audio format`

Dosyanın uzantısının desteklenen listede olduğunu kontrol et. Yalnızca uzantıyı yeniden
adlandırmak codec'i dönüştürmez.

### `No playable beats were detected`

Dosya sessiz, çok düşük seviyeli veya belirgin onset içermiyor olabilir. Başka bir kayıt
dene; ayrıntılı ayar ve postprocess için Editor Python pipeline'ını kullan.

### Build açılıyor fakat özel şarkı seçilemiyor

Bu sürüm Windows dosya seçicisi kullanır. macOS ve Linux standalone build'leri bu
özelliğin mevcut sürümünde desteklenmez.

## FFmpeg dağıtım notu

FFmpeg projesi kaynak kodunu sağlar ve Windows executable bağlantılarını resmi indirme
sayfasında üçüncü taraf sağlayıcılara yönlendirir. Dağıttığın kesin binary'nin LGPL/GPL
yapılandırmasını kontrol et; tester build'iyle birlikte gereken bildirimleri ve eşleşen
kaynak koduna erişimi sağla. Bu bölüm hukuki tavsiye değildir.

- FFmpeg indirme: https://ffmpeg.org/download.html
- FFmpeg lisans bilgisi: https://ffmpeg.org/legal.html
- Gyan Windows builds: https://www.gyan.dev/ffmpeg/builds/

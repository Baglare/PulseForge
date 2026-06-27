# PulseForge Debug Audio Analyzer

Bu klasor PulseForge icin ilk debug audio analyzer araclarini icerir.

## Ne yapar?

- PCM WAV dosyasindaki belirgin click/transient zamanlarini bulur.
- PulseForge Unity debug beatmap JSON formatinda cikti uretir.
- Deterministic debug click track WAV dosyasi uretebilir.
- Sadece Python standard library kullanir.

## Ne yapmaz?

- Final muzik analiz sistemi degildir.
- MP3 veya sikistirilmis format okumaz.
- `librosa`, `numpy`, `scipy`, `ffmpeg`, `pydub` veya harici paket kullanmaz.
- Tempo, beat grid, bar/measure veya muzik yapisi tahmini yapmaz.
- Unity tarafinda runtime analiz veya UI saglamaz.

## Debug click track nasil uretilir?

`generate_debug_click_track.py`, verilen zamanlarda kisa 16-bit PCM sine burst click'leri yazar.

```powershell
python tools/audio_analyzer/generate_debug_click_track.py `
  --output Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --times 1.00,1.50,2.00,2.50,3.00,3.25,3.75,4.25,4.75,5.25
```

Varsayilanlar:

- `--sample-rate 44100`
- `--click-duration-ms 25`
- `--click-frequency-hz 1000`
- `--amplitude 0.8`
- `--channels 1`

Output klasoru yoksa otomatik olusturulur. `--duration-seconds` verilmezse sure son click zamanindan en az 1 saniye sonrasina kadar hesaplanir.

## Uretilen WAV analyzer'a nasil verilir?

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM.json `
  --display-name "Analyzed Debug 120 BPM" `
  --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike
```

`--output` verilmezse JSON stdout'a yazilir.

## Diagnostics report nasil uretilir?

Analyzer, beatmap JSON disinda ayri bir analysis report JSON dosyasi yazabilir. Bu dosya Unity'ye verilmez; analyzer'in hangi WAV ayarlariyla kac event sectigini denetlemek icindir.

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM.json `
  --report-output tools/audio_analyzer/out/BM_Analyzed_Debug_120BPM.report.json `
  --summary `
  --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike
```

Report JSON en az input path, display name, sample rate, channel count, sample width, duration, analyzer ayarlari, max frame amplitude, detected event count ve secilen event listesini icerir.

## Debug CSV ne ise yarar?

`--debug-csv-output`, frame bazli analyzer debug tablosu yazar. Bu dosya da Unity'ye verilmez; threshold ve min-gap ayarlarinin neden belirli peak'leri sectigini anlamak icindir.

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM.json `
  --report-output tools/audio_analyzer/out/BM_Analyzed_Debug_120BPM.report.json `
  --debug-csv-output tools/audio_analyzer/out/BM_Analyzed_Debug_120BPM.frames.csv `
  --summary `
  --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike
```

CSV kolonlari:

- `frameIndex`
- `timeSeconds`
- `amplitude`
- `isLocalPeak`
- `isSelectedPeak`

`tools/audio_analyzer/out/` local diagnostics klasorudur ve `.gitignore` icindedir.

## Beatmap comparison nasil yapilir?

`compare_beatmaps.py`, expected/reference beatmap ile generated/actual beatmap arasindaki event zaman farklarini index sirasina gore olcer. Bu arac Unity'ye veri uretmez; timing debug ve analyzer kalibrasyonu icindir.

```powershell
python tools/audio_analyzer/compare_beatmaps.py `
  Assets/PulseForge/Demo/BeatMaps/BM_Debug_120BPM_Default.json `
  Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM.json `
  --tolerance-ms 40 `
  --report-output tools/audio_analyzer/out/compare_debug_120bpm.json
```

Console summary su bilgileri yazar:

- expected/actual/compared event count
- missing ve extra event count
- action mismatch count
- tolerance ici/disi event count
- mean signed error ms
- mean absolute error ms
- max absolute error ms
- suggested global offset seconds

`suggestedGlobalOffsetSeconds`, `-meanSignedErrorSeconds` olarak hesaplanir. Actual eventler expected eventlerden 10 ms gec ise mean signed error yaklasik `+10 ms` olur ve suggested offset yaklasik `-0.010` olur. Bu, actual beatmap'i daha erkene cekmek icin kullanilabilecek global offset degeridir.

`--strict` verilmezse count mismatch veya tolerance asimi komutu hatali bitirmez; sadece summary raporlar. `--strict` verilirse count mismatch, action mismatch veya tolerance disi event varsa komut non-zero exit code ile biter.

Report JSON dosyasi Unity'ye verilmez. Sadece comparison/debug icindir ve `tools/audio_analyzer/out/` altina yazilabilir.

## Unity'ye nasil verilir?

1. Uretilen `.wav` dosyasini Unity projesinde `Assets/` altinda bir klasore koy.
2. Uretilen `.json` dosyasini Unity projesinde `Assets/` altinda bir klasore koy.
3. Unity import ettikten sonra JSON dosyasi `TextAsset` olarak gorunur.
4. Debug prototype objesindeki `Debug Beat Map Json` alanina JSON TextAsset'i ata.
5. Debug prototype objesindeki `Debug Audio Clip` alanina WAV AudioClip'i ata.
6. Play Mode'da `Start / Restart` ile countdown biter, audio clip baslar ve beatmap JSON'dan okunur.

## Uctan uca ornek

```powershell
python tools/audio_analyzer/generate_debug_click_track.py `
  --output Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --times 1.00,1.50,2.00,2.50,3.00,3.25,3.75,4.25,4.75,5.25

python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM.json `
  --display-name "Analyzed Debug 120 BPM" `
  --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike
```

## Analyzer ayarlari

- `--frame-ms`: Analiz frame suresi. Varsayilan `10`.
- `--threshold-ratio`: En yuksek amplitude'a gore peak esigi. Varsayilan `0.35`.
- `--min-gap-seconds`: Iki peak arasindaki minimum sure. Varsayilan `0.18`.
- `--max-events`: Uretilecek maksimum event sayisi.
- `--pattern`: Event action sirasi. Sadece `Guard` ve `Strike` kabul edilir; pattern biterse bastan doner.
- `--global-offset-seconds`: JSON icindeki global offset alani.

## Ilk surum sinirlamalari

- En iyi sonucu click track veya cok belirgin transient iceren debug seslerde verir.
- Gurultulu muzikte yanlis peak secebilir.
- Cok sik transientlerde `--min-gap-seconds` ayari sonucu ciddi sekilde etkiler.
- Float WAV destegi hedeflenmemistir.

## Test

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

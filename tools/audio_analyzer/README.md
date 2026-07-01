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
- `onsetStrength`
- `detectionValue`
- `isLocalPeak`
- `isSelectedPeak`

`amplitude` modunda `detectionValue` amplitude degeridir. `onset` modunda `detectionValue` onsetStrength degeridir.

`tools/audio_analyzer/out/` local diagnostics klasorudur ve `.gitignore` icindedir.

## Detection mode nasil secilir?

Analyzer iki detection mode destekler:

- `amplitude`: Varsayilan moddur. Frame icindeki peak amplitude degerlerini kullanir. Click track ve cok belirgin transient iceren debug seslerde en deterministik davranistir.
- `onset`: Her frame icin gecmis baseline ortalamasina gore ani artis degerini hesaplar. Click track disindaki daha muzik benzeri WAV dosyalarinda transientleri denemek icindir.

Amplitude mode eski davranisi korumak icin default kalir:

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM.json `
  --detection-mode amplitude
```

Onset mode ornegi:

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM_Onset.json `
  --detection-mode onset `
  --baseline-ms 120 `
  --onset-smooth-frames 1 `
  --threshold-ratio 0.35 `
  --min-gap-seconds 0.18 `
  --report-output tools/audio_analyzer/out/onset_debug_120bpm.report.json `
  --debug-csv-output tools/audio_analyzer/out/onset_debug_120bpm.frames.csv
```

Gercek muzik benzeri WAV dosyalarinda:

- `--threshold-ratio` cok dusukse fazla event yakalanabilir; cok yuksekse zayif vuruslar kacabilir.
- `--min-gap-seconds` cok dusukse ayni vurus birden fazla event olabilir; cok yuksekse hizli vuruslar kacabilir.
- `--baseline-ms` onset modunda gecmis enerji ortalamasinin ne kadar geriye bakacagini belirler. Daha buyuk deger daha yavas degisen baseline verir.
- `--onset-smooth-frames 0` smoothing kapatir. Daha yuksek deger onsetStrength egrisini yumusatir ama peak zamanini biraz genisletebilir.
- Debug CSV'de `onsetStrength` ve `detectionValue` kolonlarini inceleyerek threshold'un neden belirli frame'leri sectigini gorebilirsin.

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

## Playable beatmap post-process nasil yapilir?

`postprocess_beatmap.py`, analyzer tarafindan uretilen raw beatmap JSON'u daha oynanabilir bir combat beatmap JSON'a donusturur. Raw analyzer output ses transientlerini yakalamaya odaklanir; playable beatmap ise minimum aralik ve action mapping kurallariyla oyuncunun takip edebilecegi event listesi uretir.

Raw analyzer output uret:

```powershell
python tools/audio_analyzer/pulseforge_audio_analyzer.py `
  Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM_Onset.json `
  --detection-mode onset `
  --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike
```

Playable beatmap uret:

```powershell
python tools/audio_analyzer/postprocess_beatmap.py `
  Assets/PulseForge/Demo/BeatMaps/BM_Analyzed_Debug_120BPM_Onset.json `
  --output Assets/PulseForge/Demo/BeatMaps/BM_Playable_Debug_120BPM.json `
  --display-name "Playable Debug 120 BPM" `
  --difficulty normal `
  --action-mode pattern `
  --pattern Guard,Guard,Strike,Guard,Strike `
  --report-output tools/audio_analyzer/out/postprocess_debug_120bpm.report.json
```

`--output` verilmezse playable beatmap JSON stdout'a yazilir. `--report-output` dosyasi Unity'ye verilmez; post-process debug icindir.

Difficulty presetleri minimum event araligini belirler:

- `easy`: `minGapSeconds = 0.45`
- `normal`: `minGapSeconds = 0.28`
- `hard`: `minGapSeconds = 0.18`

`--min-gap-seconds` verilirse difficulty preset degerini override eder. Cok yakin eventlerde daha yuksek intensity event tutulur; intensity esit ise daha erken event kalir.

Action mode secenekleri:

- `preserve`: Input event action degerini korur.
- `alternate`: Guard, Strike, Guard, Strike sirasiyla uretir.
- `pattern`: `--pattern` listesini dongu halinde uygular.
- `intensity`: `--intensity-strike-threshold` ve ustunu Strike, altini Guard yapar.

## Combat-style preset nedir?

`--combat-style`, raw analyzer eventlerini secilen dovus hissine gore Guard / Strike dizisine cevirir. Bu sistem final combat generator degildir; debug prototype icin ilk deterministik preset katmanidir. Output JSON yine `schemaVersion: 1` kullanir ve Unity tarafindaki `DebugBeatMapJsonParser` icin yeni alan zorunlulugu getirmez.

Preset secenekleri:

- `legacy`: Eski `--action-mode` davranisini korur. Varsayilandir.
- `balanced`: Dengeli parry/slash dizisi uretir. Temel tekrar pattern'i `Guard, Guard, Strike, Guard, Strike, Strike`.
- `defensive`: Guard agirlikli savunmaci dizi uretir. Dusuk/orta intensity eventler Guard kalir; yuksek intensity eventler sinirli Strike uretebilir.
- `aggressive`: Strike agirlikli dizi uretir. Temel tekrar pattern'i `Strike, Strike, Guard, Strike`.
- `bursty`: Yuksek intensity veya birbirine yakin eventleri kisa saldiri patlamasi gibi Strike'a yaklastirir.

`action-mode` ile `combat-style` farki:

- `action-mode` mekanik mapping icindir: preserve, alternate, pattern veya intensity.
- `combat-style` oynanis hissi icindir: dengeli, savunmaci, saldirgan veya patlamali Guard/Strike dagilimi.
- `--combat-style legacy` disinda bir preset kullaniliyorsa `--action-mode` ve `--pattern` verme. Bu durumda action mapping preset tarafindan yapilir.

Hangi durumda hangisi kullanilmali?

- Eski pipeline davranisini aynen korumak icin `legacy`.
- Genel demo ve portfolyo akisi icin `balanced`.
- Parry agirlikli sahne feedback'i gostermek icin `defensive`.
- Slash agirlikli, daha saldirgan demo icin `aggressive`.
- Sakin bolumler ve kisa saldiri patlamalari gostermek icin `bursty`.

Postprocess ornegi:

```powershell
python tools/audio_analyzer/postprocess_beatmap.py raw.json `
  --output playable.json `
  --combat-style balanced `
  --difficulty normal
```

Bursty preset icin yakin event penceresi ayarlanabilir:

```powershell
python tools/audio_analyzer/postprocess_beatmap.py raw.json `
  --output playable.json `
  --combat-style bursty `
  --burst-window-seconds 0.35 `
  --difficulty hard
```

Unity'de kullanmak icin post-process sonucu olusan playable JSON dosyasini Debug prototype objesindeki `Debug Beat Map Json` alanina ata. Report JSON dosyasini Unity'ye verme.

## Style variant generator ne ise yarar?

`generate_style_variants.py`, ayni input WAV veya mevcut raw beatmap JSON uzerinden birden fazla combat-style playable beatmap uretir. Amac, ayni ritim analizini farkli dovus koreografileriyle karsilastirmaktir.

Varsayilan olarak su playable JSON dosyalari uretilir:

- `BM_Playable_<name>_Balanced.json`
- `BM_Playable_<name>_Defensive.json`
- `BM_Playable_<name>_Aggressive.json`
- `BM_Playable_<name>_Bursty.json`

WAV input ile ornek:

```powershell
python tools/audio_analyzer/generate_style_variants.py `
  --input-wav Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output-dir Assets/PulseForge/Demo/BeatMaps `
  --name Debug_120BPM `
  --difficulty hard `
  --detection-mode amplitude `
  --summary
```

Mevcut raw JSON ile ornek:

```powershell
python tools/audio_analyzer/generate_style_variants.py `
  --input-raw-json Assets/PulseForge/Demo/BeatMaps/BM_Raw_Debug_120BPM.json `
  --output-dir Assets/PulseForge/Demo/BeatMaps `
  --name Debug_120BPM `
  --difficulty hard
```

Farkli ciktilarin anlami:

- `Balanced`: Dengeli Guard / Strike akisi.
- `Defensive`: Guard agirlikli, parry odakli akis.
- `Aggressive`: Strike agirlikli, slash odakli akis.
- `Bursty`: Yuksek intensity veya yakin eventlerde saldiri patlamasi hissi.

Unity'de denemek icin bu playable JSON dosyalarindan birini `DebugRhythmPrototypeController` uzerindeki `Debug Beat Map Json` alanina ata. Raw JSON dosyasi analiz/debug kaynagidir; prototype'a asil atanacak dosya `BM_Playable_<name>_<Style>.json` dosyasidir.

Her style icin `tools/audio_analyzer/out/<name>_<style>_postprocess_report.json` uretilir. `--expected-json` verilirse ayrica `<name>_<style>_compare_report.json` olusur. Bu raporlar debug amaclidir, Unity'ye verilmez ve commitlenmemelidir.

## Tek komutluk debug pipeline nasil calistirilir?

`run_debug_pipeline.py`, WAV dosyasindan raw analyzer beatmap, playable beatmap ve diagnostics raporlarini tek komutla uretir. Istege bagli olarak expected/reference beatmap ile playable sonucu karsilastirir.

```powershell
python tools/audio_analyzer/run_debug_pipeline.py `
  --input-wav Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output-dir Assets/PulseForge/Demo/BeatMaps `
  --name Debug_120BPM `
  --pattern Guard,Guard,Strike,Guard,Strike,Strike,Guard,Strike,Guard,Strike `
  --detection-mode amplitude `
  --difficulty hard `
  --expected-json Assets/PulseForge/Demo/BeatMaps/BM_Debug_120BPM_Default.json `
  --write-debug-csv `
  --summary
```

Combat-style preset ile tek komutluk ornek:

```powershell
python tools/audio_analyzer/run_debug_pipeline.py `
  --input-wav Assets/PulseForge/Demo/Audio/PF_Debug_120BPM_DefaultBeatMap.wav `
  --output-dir Assets/PulseForge/Demo/BeatMaps `
  --name Debug_120BPM_Balanced `
  --combat-style balanced `
  --difficulty hard
```

Pipeline su dosyalari uretir:

- `BM_Raw_<name>.json`: Analyzer tarafindan uretilen raw beatmap.
- `BM_Playable_<name>.json`: Postprocess sonrasi Unity'ye atanacak playable beatmap.
- `tools/audio_analyzer/out/<name>_analysis_report.json`: Analyzer diagnostics report.
- `tools/audio_analyzer/out/<name>_postprocess_report.json`: Postprocess report.
- `tools/audio_analyzer/out/<name>_compare_report.json`: Sadece `--expected-json` verilirse comparison report.
- `tools/audio_analyzer/out/<name>_frames.csv`: Sadece `--write-debug-csv` verilirse frame debug CSV.

Unity'de `BM_Playable_<name>.json` dosyasini Debug prototype objesindeki `Debug Beat Map Json` alanina ata. Raw JSON analiz/debug icindir; playable olmayan fazla sik eventler icerebilir. `tools/audio_analyzer/out/` altindaki raporlar ve CSV dosyalari Unity'ye verilmez ve commitlenmemelidir.

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
- `--detection-mode`: `amplitude` veya `onset`. Varsayilan `amplitude`.
- `--baseline-ms`: Onset modunda gecmis baseline penceresi. Varsayilan `120`.
- `--onset-smooth-frames`: OnsetStrength smoothing yaricapi. `0` smoothing kapatir. Varsayilan `1`.

## Ilk surum sinirlamalari

- En iyi sonucu click track veya cok belirgin transient iceren debug seslerde verir.
- Gurultulu muzikte yanlis peak secebilir.
- Cok sik transientlerde `--min-gap-seconds` ayari sonucu ciddi sekilde etkiler.
- Float WAV destegi hedeflenmemistir.

## Test

```powershell
python -m unittest discover tools/audio_analyzer/tests
```

using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Persistence;

namespace PulseForge.Runtime.Unity.Onboarding
{
    public static class PulseForgeM9HLocalization
    {
        public static string Text(string key, PulseForgeUILanguage language)
        {
            bool tr = language == PulseForgeUILanguage.Turkish;
            switch (key)
            {
                case "FirstTimeSetup": return tr ? "İlk Kurulum" : "First-Time Setup";
                case "Language": return tr ? "Dil" : "Language";
                case "ReadabilityProfile": return tr ? "Okunabilirlik Profili" : "Readability Profile";
                case "BindingSummary": return tr ? "Tuş Özeti" : "Binding Summary";
                case "Calibration": return tr ? "Kalibrasyon" : "Calibration";
                case "BasicTraining": return tr ? "Temel Eğitim" : "Basic Training";
                case "Complete": return tr ? "Tamamla" : "Complete";
                case "LanguageDescription": return tr
                    ? "Arayüz dilini seç. Değişiklik açık akışa hemen yansır."
                    : "Choose the UI language. The active flow updates immediately.";
                case "ReadabilityDescription": return tr
                    ? "Görsel okunabilirlik için bir başlangıç profili seç. Ayarları daha sonra tek tek değiştirebilirsin."
                    : "Choose a starting readability profile. Every setting remains individually editable.";
                case "BindingsDescription": return tr
                    ? "Eğitim ve kalibrasyon mevcut atanmış tuşları kullanır."
                    : "Calibration and training use the currently assigned bindings.";
                case "CalibrationDescription": return tr
                    ? "Önce click ve görsel pulse hizasını, sonra Guard input zamanlamasını ayarla."
                    : "Align the click and visual pulse, then measure Guard input timing.";
                case "BasicTrainingDescription": return tr
                    ? "Timing Bar, Guard, Dodge, Light ve Heavy derslerini tamamla."
                    : "Complete Timing Bar, Guard, Dodge, Light, and Heavy lessons.";
                case "CompleteDescription": return tr
                    ? "Kurulum hazır. Setup ekranına geçebilirsin."
                    : "Setup is ready. Continue to the Setup screen.";
                case "Standard": return tr ? "Standart" : "Standard";
                case "Assisted": return tr ? "Destekli (Önerilen)" : "Assisted (Recommended)";
                case "HighClarity": return tr ? "Yüksek Netlik" : "High Clarity";
                case "Back": return tr ? "Geri" : "Back";
                case "Next": return tr ? "İleri" : "Next";
                case "SkipSetup": return tr ? "Kurulumu Atla" : "Skip Setup";
                case "ConfirmSkip": return tr ? "Atlamayı Onayla" : "Confirm Skip";
                case "SkipWarning": return tr
                    ? "Kalibrasyon ve temel eğitim atlanacak. Bunları daha sonra Settings ekranından açabilirsin."
                    : "Calibration and basic training will be skipped. You can run them later from Settings.";
                case "Cancel": return tr ? "İptal" : "Cancel";
                case "StartCalibration": return tr ? "Kalibrasyonu Başlat" : "Start Calibration";
                case "StartBasicTraining": return tr ? "Temel Eğitimi Başlat" : "Start Basic Training";
                case "CompleteSetup": return tr ? "Kurulumu Tamamla" : "Complete Setup";
                case "AudioVisualAlignment": return tr ? "Ses / Görsel Hizalama" : "Audio / Visual Alignment";
                case "AudioVisualDescription": return tr
                    ? "Click ile judgement-ring benzeri pulse hizalanana kadar 5 ms adımlarla ayarla."
                    : "Adjust in 5 ms steps until the click and judgement-ring-style pulse align.";
                case "Apply": return tr ? "Uygula" : "Apply";
                case "KeepCurrent": return tr ? "Mevcut Değeri Koru" : "Keep Current";
                case "Retry": return tr ? "Tekrar Dene" : "Retry";
                case "InputTimingCalibration": return tr ? "Input Zamanlama Kalibrasyonu" : "Input Timing Calibration";
                case "InputCalibrationDescription": return tr
                    ? "İki ısınma beat’inden sonra on iki beat boyunca duyduğun anda Guard tuşuna bas."
                    : "After two warm-up beats, press Guard when you hear each of twelve measured beats.";
                case "StartMeasurement": return tr ? "Ölçümü Başlat" : "Start Measurement";
                case "Measuring": return tr ? "Ölçülüyor" : "Measuring";
                case "SuggestedOffset": return tr ? "Önerilen offset" : "Suggested offset";
                case "ValidSamples": return tr ? "Geçerli örnek" : "Valid samples";
                case "MedianDeviation": return tr ? "Median sapma" : "Median deviation";
                case "Jitter": return "Jitter";
                case "Confidence": return tr ? "Güven" : "Confidence";
                case "Low": return tr ? "Düşük" : "Low";
                case "Medium": return tr ? "Orta" : "Medium";
                case "High": return tr ? "Yüksek" : "High";
                case "NotEnoughSamples": return tr
                    ? "Sonuç için en az 8 geçerli örnek gerekir."
                    : "At least 8 valid samples are required for a result.";
                case "ApplySuggested": return tr ? "Önerileni Uygula" : "Apply Suggested";
                case "Training": return tr ? "Eğitim" : "Training";
                case "BasicLessons": return tr ? "Temel Dersler" : "Basic Lessons";
                case "AdvancedLessons": return tr ? "Gelişmiş Dersler" : "Advanced Lessons";
                case "ExitTraining": return tr ? "Eğitimden Çık" : "Exit Training";
                case "NextLesson": return tr ? "Sonraki Ders" : "Next Lesson";
                case "LessonComplete": return tr ? "Ders Tamamlandı" : "Lesson Complete";
                case "SuccessfulAttempts": return tr ? "Başarılı tekrar" : "Successful attempts";
                case "TimingBarLegend": return tr
                    ? "Yeşil = Good, altın = Perfect, beyaz çizgi = tam beat merkezi."
                    : "Green = Good, gold = Perfect, white line = exact beat center.";
                case "TimingBarInstruction": return tr
                    ? "Mavi işaret beyaz çizgiye geldiğinde bas."
                    : "Press when the blue marker reaches the white line.";
                case "BeatIn": return tr ? "Beat’e kalan" : "Beat in";
                case "Now": return tr ? "ŞİMDİ" : "NOW";
                case "TimingBarEarly": return tr ? "Örnek 1/3: Beyaz çizgiden erken bas." : "Example 1/3: Press before the white line.";
                case "TimingBarGood": return tr ? "Örnek 2/3: Yeşil Good aralığına bas." : "Example 2/3: Press inside the green Good range.";
                case "TimingBarPerfect": return tr ? "Örnek 3/3: Altın Perfect aralığına bas." : "Example 3/3: Press inside the gold Perfect range.";
                case "TwoSuccesses": return tr ? "Dersi tamamlamak için iki başarılı tekrar yap." : "Complete two successful repetitions.";
                case "TooEarly": return tr ? "Çok Erken" : "Too Early";
                case "TooLate": return tr ? "Çok Geç" : "Too Late";
                case "WrongKey": return tr ? "Yanlış Tuş" : "Wrong Key";
                case "ReleasedTooSoon": return tr ? "Çok Erken Bırakıldı" : "Released Too Soon";
                case "ReleasedTooLate": return tr ? "Çok Geç Bırakıldı" : "Released Too Late";
                case "MissingSecondInput": return tr ? "İkinci Input Eksik" : "Missing Second Input";
                case "WrongSequenceOrder": return tr ? "Yanlış Sıra" : "Wrong Sequence Order";
                case "PressOnBeat": return tr ? "Beat üzerinde bas." : "Press on the beat.";
                case "HoldUntilRelease": return tr ? "Basılı tut ve release işaretinde bırak." : "Hold, then release on the release cue.";
                case "HoldGuard": return tr ? "Guard’ı beat üzerinde basılı tut." : "Hold Guard from the beat.";
                case "GuardHoldStart": return tr
                    ? "İlk beyaz çizgide Guard’a bas ve bırakma."
                    : "Press Guard at the first white line and do not release.";
                case "GuardHoldContinue": return tr
                    ? "Basılı tut. İkinci çizgide otomatik tamamlanacak."
                    : "Keep holding. It completes automatically at the second line.";
                case "ChooseOne": return tr ? "Guard veya Dodge seçeneklerinden birini zamanında kullan." : "Use either Guard or Dodge on time.";
                case "PressTogether": return tr ? "İki tuşa birlikte bas." : "Press both inputs together.";
                case "FollowOrder": return tr ? "Tuşları gösterilen sırayla kullan." : "Use the bindings in the shown order.";
                case "FollowChain": return tr ? "Kısa Light Attack zincirini takip et." : "Follow the short Light Attack chain.";
                case "BreakQuickly": return tr ? "Hedefi süre içinde art arda Light Attack ile kır." : "Break the target with repeated Light Attacks before the deadline.";
                default: return key ?? string.Empty;
            }
        }

        public static string LessonName(TrainingLessonId lessonId, PulseForgeUILanguage language)
        {
            bool tr = language == PulseForgeUILanguage.Turkish;
            switch (lessonId)
            {
                case TrainingLessonId.TimingBar: return "Timing Bar";
                case TrainingLessonId.GuardTap: return tr ? "Guard Dokunuşu" : "Guard Tap";
                case TrainingLessonId.DodgeTap: return tr ? "Dodge Dokunuşu" : "Dodge Tap";
                case TrainingLessonId.LightAttack: return "Light Attack";
                case TrainingLessonId.HeavyChargeRelease: return tr ? "Heavy Şarj ve Bırakma" : "Heavy Charge Release";
                case TrainingLessonId.GuardHold: return tr ? "Guard Basılı Tutma" : "Guard Hold";
                case TrainingLessonId.Choice: return tr ? "Seçim" : "Choice";
                case TrainingLessonId.Chord: return "Chord";
                case TrainingLessonId.OrderedSequence: return tr ? "Sıralı Dizi" : "Ordered Sequence";
                case TrainingLessonId.SwarmChain: return tr ? "Swarm Zinciri" : "Swarm Chain";
                default: return tr ? "Hedef Kırma" : "Break Target";
            }
        }

        public static string LessonDescription(TrainingLessonId lessonId, PulseForgeUILanguage language)
        {
            switch (lessonId)
            {
                case TrainingLessonId.TimingBar: return Text("TimingBarLegend", language);
                case TrainingLessonId.GuardTap:
                case TrainingLessonId.DodgeTap:
                case TrainingLessonId.LightAttack: return Text("PressOnBeat", language);
                case TrainingLessonId.HeavyChargeRelease: return Text("HoldUntilRelease", language);
                case TrainingLessonId.GuardHold: return Text("HoldGuard", language);
                case TrainingLessonId.Choice: return Text("ChooseOne", language);
                case TrainingLessonId.Chord: return Text("PressTogether", language);
                case TrainingLessonId.OrderedSequence: return Text("FollowOrder", language);
                case TrainingLessonId.SwarmChain: return Text("FollowChain", language);
                default: return Text("BreakQuickly", language);
            }
        }
    }
}

using System;

namespace EasyTABS.Services
{
    /// <summary>
    /// Зафіксований (стабільний) стан тюнера, який виводиться на екран.
    /// </summary>
    public class StableReading
    {
        public string Note { get; init; } = "--";
        public double Frequency { get; init; }
        public double Cents { get; init; }
        public string Chord { get; init; } = "—";
        public double[] Chromagram { get; init; } = new double[12];
        public bool IsLocked { get; init; }   // true — значення зафіксоване (звук зловлено)
    }

    /// <summary>
    /// Реалізує «фіксацію» тюнера: замість миготіння кожного кадру
    /// тримає значення, поки кілька кадрів поспіль згодні між собою.
    /// Коли звук затихає (кадри стають беззвучними) — фіксація скидається
    /// не одразу, а через невелику затримку (release), щоб напис не зникав
    /// миттєво між коливаннями струни.
    /// </summary>
    public class StabilityTracker
    {
        // Скільки однакових кадрів поспіль потрібно, щоб зафіксувати.
        public int FramesToLock { get; set; } = 2;
        // Скільки беззвучних кадрів стерпіти, перш ніж скинути фіксацію.
        public int FramesToRelease { get; set; } = 6;
        // Допуск для ноти (в центах) — у межах вважаємо «той самий звук».
        public double CentsTolerance { get; set; } = 30.0;

        private string _candidateNote = "--";
        private string _candidateChord = "—";
        private int _agreeCount;
        private int _silenceCount;

        private StableReading _locked = new();

        /// <summary>
        /// Подає кадр у трекер; повертає поточний (можливо зафіксований) стан.
        /// </summary>
        public StableReading Push(AudioAnalysisResult r, TunerMode mode)
        {
            // Беззвучний кадр: рахуємо тишу, але тримаємо останнє значення.
            if (!r.IsVoiced)
            {
                _silenceCount++;
                _agreeCount = 0;
                if (_silenceCount >= FramesToRelease)
                {
                    // Достатньо тиші — скидаємо.
                    _locked = new StableReading();
                    _candidateNote = "--";
                    _candidateChord = "—";
                }
                return _locked;
            }

            _silenceCount = 0;

            // Ключ для порівняння залежить від режиму.
            string key = mode == TunerMode.Chord ? r.Chord : r.Note;
            string lastKey = mode == TunerMode.Chord ? _candidateChord : _candidateNote;

            bool sameAsCandidate =
                key != "--" && key != "—" && key == lastKey;

            if (sameAsCandidate)
                _agreeCount++;
            else
            {
                _agreeCount = 1;
                _candidateNote = r.Note;
                _candidateChord = r.Chord;
            }

            // Достатньо згоди — фіксуємо нове значення.
            if (_agreeCount >= FramesToLock && key != "--" && key != "—")
            {
                _locked = new StableReading
                {
                    Note = r.Note,
                    Frequency = r.Frequency,
                    Cents = r.Cents,
                    Chord = r.Chord,
                    Chromagram = r.Chromagram,
                    IsLocked = true
                };
            }
            else if (_locked.IsLocked && mode == TunerMode.Note &&
                     r.Note == _locked.Note)
            {
                // Та сама нота тримається — плавно оновлюємо центи/частоту,
                // щоб стрілка строю рухалась, поки тягнеш струну.
                _locked = new StableReading
                {
                    Note = r.Note,
                    Frequency = r.Frequency,
                    Cents = r.Cents,
                    Chord = _locked.Chord,
                    Chromagram = r.Chromagram,
                    IsLocked = true
                };
            }

            return _locked;
        }

        public void Reset()
        {
            _candidateNote = "--";
            _candidateChord = "—";
            _agreeCount = 0;
            _silenceCount = 0;
            _locked = new StableReading();
        }
    }
}

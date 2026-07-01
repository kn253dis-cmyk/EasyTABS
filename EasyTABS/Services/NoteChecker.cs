namespace EasyTABS.Services
{
    public enum NoteCheckState
    {
        Idle,       // немає активної ноти для перевірки
        Waiting,    // очікуємо, поки гравець зіграє (жовтий)
        Correct,    // зіграно правильно (зелений)
        Wrong       // чути звук, але не той (червоний)
    }

    // Живий стан мікрофона — для видимого фідбеку незалежно від того,
    // чи зараз відтворюється таб.
    public class MicFeedback
    {
        public bool IsListening { get; init; }
        public bool HasSignal { get; init; }   // чи чути звук зараз
        public string Note { get; init; } = "--";
        public double Frequency { get; init; }
        public NoteCheckState CheckState { get; init; } = NoteCheckState.Idle;
    }

    /// <summary>
    /// Слухає мікрофон через TunerService і дає два види фідбеку:
    ///  1) живий — яку ноту чути зараз (показуємо завжди, коли мікрофон активний);
    ///  2) перевірку — чи збігається зіграна нота з очікуваною від AlphaTab.
    /// Логіка перевірки портована з WPF AudioChecker: допуск ±5%, октавні збіги.
    /// </summary>
    public class NoteChecker
    {
        private readonly TunerService _tuner;
        private double _expectedHz;
        private volatile bool _isChecking;
        private int _checkId;

        private NoteCheckState _lastCheckState = NoteCheckState.Idle;

        // Живий фідбек мікрофона (нота, наявність сигналу).
        public event Action<MicFeedback>? FeedbackChanged;

        public bool IsRunning => _tuner.IsRunning;

        public NoteChecker(TunerService tuner)
        {
            _tuner = tuner;
            _tuner.FrameReady += OnFrame;
        }

        public void Start() => _tuner.Start();

        public void Stop()
        {
            _isChecking = false;
            _tuner.Stop();
            _lastCheckState = NoteCheckState.Idle;
            FeedbackChanged?.Invoke(new MicFeedback { IsListening = false });
        }

        // Викликається, коли AlphaTab повідомляє про активну ноту (MIDI).
        // midi < 0 -> зараз нот немає, перевірку не робимо (але слухати продовжуємо).
        public void SetExpectedMidi(int midi)
        {
            if (midi < 0)
            {
                _isChecking = false;
                _expectedHz = 0;
                _lastCheckState = NoteCheckState.Idle;
                return;
            }

            _expectedHz = MidiToHz(midi);
            _isChecking = true;
            _lastCheckState = NoteCheckState.Waiting;

            // Вікно перевірки — 250 мс, як у WPF-версії.
            var id = System.Threading.Interlocked.Increment(ref _checkId);
            _ = Task.Run(async () =>
            {
                await Task.Delay(250);
                if (_checkId == id)
                {
                    _isChecking = false;
                    if (_lastCheckState == NoteCheckState.Waiting)
                        _lastCheckState = NoteCheckState.Idle;
                }
            });
        }

        private void OnFrame(AudioAnalysisResult result)
        {
            var hasSignal = result.IsVoiced && result.Frequency >= 40;

            // Оновлюємо стан перевірки, якщо є що перевіряти.
            if (_isChecking && _expectedHz > 0 && hasSignal)
            {
                var correct = IsPitchCorrect(_expectedHz, result.Frequency);
                _lastCheckState = correct ? NoteCheckState.Correct : NoteCheckState.Wrong;
            }

            // Живий фідбек шлемо завжди, поки слухаємо.
            FeedbackChanged?.Invoke(new MicFeedback
            {
                IsListening = true,
                HasSignal = hasSignal,
                Note = hasSignal ? result.Note : "--",
                Frequency = hasSignal ? result.Frequency : 0,
                CheckState = _lastCheckState
            });
        }

        private static double MidiToHz(double midiNote)
            => 440.0 * Math.Pow(2.0, (midiNote - 69.0) / 12.0);

        private static bool IsPitchCorrect(double expected, double actual)
        {
            if (Math.Abs(expected - actual) <= expected * 0.05) return true;
            var lower = expected / 2.0;
            if (Math.Abs(lower - actual) <= lower * 0.05) return true;
            var higher = expected * 2.0;
            if (Math.Abs(higher - actual) <= higher * 0.05) return true;
            return false;
        }
    }
}

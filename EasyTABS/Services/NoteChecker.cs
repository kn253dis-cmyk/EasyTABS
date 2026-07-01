namespace EasyTABS.Services
{
    public enum NoteCheckState
    {
        Idle,       // немає активної ноти
        Waiting,    // очікуємо, поки гравець зіграє (жовтий)
        Correct,    // зіграно правильно (зелений)
        Wrong       // чути звук, але не той (червоний)
    }

    /// <summary>
    /// Порівнює висоту, зіграну на реальній гітарі (з мікрофона через TunerService),
    /// з очікуваною нотою, яку зараз програє AlphaTab (MIDI від activeBeatsChanged).
    /// Логіка портована з WPF AudioChecker: допуск ±5%, врахування октавних збігів.
    /// </summary>
    public class NoteChecker
    {
        private readonly TunerService _tuner;
        private double _expectedHz;
        private volatile bool _isChecking;
        private int _checkId;

        public event Action<NoteCheckState>? StateChanged;

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
            StateChanged?.Invoke(NoteCheckState.Idle);
        }

        // Викликається, коли AlphaTab повідомляє про активну ноту (MIDI).
        // midi < 0 -> зараз нот немає.
        public void SetExpectedMidi(int midi)
        {
            if (midi < 0)
            {
                _isChecking = false;
                _expectedHz = 0;
                StateChanged?.Invoke(NoteCheckState.Idle);
                return;
            }

            _expectedHz = MidiToHz(midi);
            _isChecking = true;
            StateChanged?.Invoke(NoteCheckState.Waiting);

            // Вікно перевірки — 250 мс, як у WPF-версії.
            var id = System.Threading.Interlocked.Increment(ref _checkId);
            _ = Task.Run(async () =>
            {
                await Task.Delay(250);
                if (_checkId == id)
                {
                    _isChecking = false;
                    StateChanged?.Invoke(NoteCheckState.Idle);
                }
            });
        }

        private void OnFrame(AudioAnalysisResult result)
        {
            // Тиша або немає активної перевірки — ігноруємо.
            if (!_isChecking || _expectedHz <= 0 || result.Frequency < 40)
                return;

            var correct = IsPitchCorrect(_expectedHz, result.Frequency);
            StateChanged?.Invoke(correct ? NoteCheckState.Correct : NoteCheckState.Wrong);
        }

        private static double MidiToHz(double midiNote)
            => 440.0 * Math.Pow(2.0, (midiNote - 69.0) / 12.0);

        // Допуск ±5%; враховуємо збіг на октаву нижче/вище (гітара багата на гармоніки).
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

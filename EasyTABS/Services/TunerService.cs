using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasyTABS.Services
{
    /// <summary>
    /// Зв'язує захоплення звуку (IAudioCapture) з аналізатором (AudioAnalyzer)
    /// та трекером стабільності (StabilityTracker).
    /// Накопичує семпли, ріже на кадри, аналізує у фоні, фіксує стабільні значення.
    /// </summary>
    public class TunerService
    {
        private readonly IAudioCapture _capture;
        private readonly AudioAnalyzer _analyzer;
        private readonly StabilityTracker _tracker = new();
        private readonly int _hopSize;

        private readonly List<float> _buffer = new();
        private readonly object _lock = new();

        // Поточний режим (нота / акорд). Можна міняти на льоту.
        public TunerMode Mode { get; set; } = TunerMode.Note;

        // Зафіксований результат — для виводу на екран.
        public event Action<StableReading>? ReadingReady;
        // Сирий результат кадру — для «живого» спектра.
        public event Action<AudioAnalysisResult>? FrameReady;

        public bool IsRunning => _capture.IsRunning;

        public AudioAnalyzer Analyzer => _analyzer;
        public StabilityTracker Tracker => _tracker;

        public TunerService(IAudioCapture capture, int fftSize = 4096, int hopSize = 2048)
        {
            _capture = capture;
            _analyzer = new AudioAnalyzer(fftSize);
            _hopSize = hopSize;
            _capture.SamplesAvailable += OnSamples;
        }

        public void Start()
        {
            lock (_lock) _buffer.Clear();
            _tracker.Reset();
            _capture.Start();
        }

        public void Stop()
        {
            _capture.Stop();
            _tracker.Reset();
        }

        private void OnSamples(float[] samples, int count)
        {
            List<float[]> frames = new();
            int sampleRate = _capture.SampleRate;

            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                    _buffer.Add(samples[i]);

                while (_buffer.Count >= _analyzer.FftSize)
                {
                    var frame = new float[_analyzer.FftSize];
                    _buffer.CopyTo(0, frame, 0, _analyzer.FftSize);
                    _buffer.RemoveRange(0, _hopSize);
                    frames.Add(frame);
                }
            }

            foreach (var frame in frames)
            {
                var f = frame;
                var mode = Mode;
                Task.Run(() =>
                {
                    var raw = _analyzer.Analyze(f, sampleRate, mode);
                    FrameReady?.Invoke(raw);

                    // Фіксація має бути послідовною — серіалізуємо доступ до трекера.
                    StableReading stable;
                    lock (_tracker)
                        stable = _tracker.Push(raw, mode);

                    ReadingReady?.Invoke(stable);
                });
            }
        }
    }
}

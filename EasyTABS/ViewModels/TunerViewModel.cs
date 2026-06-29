using System;
using System.Linq;
using System.Windows.Input;
using EasyTABS.Services;
using Microsoft.Maui.ApplicationModel;

namespace EasyTABS.ViewModels
{
    public class TunerViewModel : BaseViewModel
    {
        private readonly TunerService _tuner;

        private string _noteText = "--";
        private string _frequencyText = "";
        private string _chordText = "—";
        private string _diagText = "";
        private double _cents;
        private double _level;          // 0..1 нормований рівень для індикатора
        private bool _isListening;
        private bool _isLocked;
        private bool _isChordMode;
        private double _sensitivity = 0.5; // 0..1 повзунок чутливості
        private double[] _chromagram = new double[12];

        public string NoteText { get => _noteText; set => SetProperty(ref _noteText, value); }
        public string FrequencyText { get => _frequencyText; set => SetProperty(ref _frequencyText, value); }
        public string ChordText { get => _chordText; set => SetProperty(ref _chordText, value); }
        public string DiagText { get => _diagText; set => SetProperty(ref _diagText, value); }
        public double Cents { get => _cents; set => SetProperty(ref _cents, value); }
        public double Level { get => _level; set => SetProperty(ref _level, value); }
        public bool IsListening { get => _isListening; set => SetProperty(ref _isListening, value); }
        public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }

        public bool IsChordMode
        {
            get => _isChordMode;
            set
            {
                if (SetProperty(ref _isChordMode, value))
                {
                    _tuner.Mode = value ? TunerMode.Chord : TunerMode.Note;
                    _tuner.Tracker.Reset();
                }
            }
        }

        // Повзунок чутливості: 0 = ловить навіть дуже тихе (низький гейт),
        // 1 = тільки гучне. Мапимо у RmsGate логарифмічно.
        public double Sensitivity
        {
            get => _sensitivity;
            set
            {
                if (SetProperty(ref _sensitivity, value))
                    ApplySensitivity();
            }
        }

        public double[] Chromagram { get => _chromagram; private set => SetProperty(ref _chromagram, value); }

        public event Action? SpectrumUpdated;

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        public TunerViewModel()
        {
            var capture = AudioCaptureFactory.Create();
            _tuner = new TunerService(capture);
            _tuner.ReadingReady += OnReadingReady;
            _tuner.FrameReady += OnFrameReady;

            // Нижчий поріг акорду — реальний сигнал рідко дає 0.65.
            _tuner.Analyzer.ChordThreshold = 0.5;

            StartCommand = new RelayCommand(async _ => await StartAsync());
            StopCommand = new RelayCommand(_ => Stop());

            ApplySensitivity();
        }

        private void ApplySensitivity()
        {
            // Sensitivity 0..1 -> RmsGate приблизно 0.0008..0.03 (логарифмічно).
            // Менша чутливість зверху повзунка = вищий гейт.
            double min = 0.0008, max = 0.03;
            double gate = min * Math.Pow(max / min, _sensitivity);
            if (_tuner != null) _tuner.Analyzer.RmsGate = gate;
        }

        private async System.Threading.Tasks.Task StartAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                NoteText = "Немає мікрофона";
                return;
            }

            try
            {
                _tuner.Start();
                IsListening = true;
            }
            catch (Exception ex)
            {
                IsListening = false;
                NoteText = "Помилка";
                DiagText = ex.Message;
                System.Diagnostics.Debug.WriteLine($"Tuner start error: {ex}");
            }
        }

        private void Stop()
        {
            _tuner.Stop();
            IsListening = false;
            IsLocked = false;
            NoteText = "--";
            FrequencyText = "";
            ChordText = "—";
            DiagText = "";
            Cents = 0;
            Level = 0;
            Chromagram = new double[12];
            SpectrumUpdated?.Invoke();
        }

        // Сирий кадр — для індикатора рівня та діагностики (показує ВСЕ, навіть нижче гейту).
        private void OnFrameReady(AudioAnalysisResult r)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!IsListening) return;

                // Нормуємо RMS у 0..1 для смужки рівня (множник підібраний емпірично).
                Level = Math.Clamp(r.Rms * 25.0, 0, 1);

                double gate = _tuner.Analyzer.RmsGate;
                string state = r.Rms >= gate ? "OK" : "тихо (підніми чутливість)";
                DiagText = $"RMS {r.Rms:0.0000} / поріг {gate:0.0000}  [{state}]";
            });
        }

        private void OnReadingReady(StableReading r)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!IsListening) return;

                IsLocked = r.IsLocked;

                if (IsChordMode)
                {
                    ChordText = r.Chord;
                    NoteText = r.Note;
                    FrequencyText = "";
                }
                else
                {
                    NoteText = r.Note;
                    FrequencyText = r.Frequency > 0 ? $"{r.Frequency:0.0} Hz" : "";
                    ChordText = "—";
                }

                Cents = r.Cents;
                Chromagram = r.Chromagram;
                SpectrumUpdated?.Invoke();
            });
        }
    }
}

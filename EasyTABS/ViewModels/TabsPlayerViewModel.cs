using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using EasyTABS.Services;

namespace EasyTABS.ViewModels
{
    // ViewModel сторінки плеєра. Id пісні приходить через QueryProperty.
    [QueryProperty(nameof(SongId), "songId")]
    public class TabsPlayerViewModel : BaseViewModel
    {
        private readonly SongRepository _repo = new();

        private AlphaTabBridge? _bridge;
        private NoteChecker? _noteChecker;

        private byte[]? _pendingTab;   // таб, завантажений з БД, чекає на готовність рушія
        private bool _engineReady;

        private int _songId;
        private string _title = string.Empty;
        private string _artistName = string.Empty;
        private byte[]? _albumCoverData;

        private bool _isBusy;
        private string _statusMessage = string.Empty;

        private bool _isPlaying;
        private bool _isMetronomeOn;
        private bool _isSynthMuted;
        private bool _isNoteCheckOn;

        private double _originalBpm = 120;
        private double _bpmMin = 60;
        private double _bpmMax = 210;
        private double _bpmValue = 120;

        private double _timelineValue;
        private double _timelineMax = 1;
        private string _timeLabel = "00:00 / 00:00";
        private bool _isDraggingTimeline;

        private Color _feedbackColor = Colors.Transparent;

        // Живий фідбек мікрофона.
        private string _micNoteText = "--";
        private string _micStatusText = string.Empty;
        private Color _micStatusColor = Colors.Transparent;

        public string MicNoteText { get => _micNoteText; set => SetProperty(ref _micNoteText, value); }
        public string MicStatusText { get => _micStatusText; set => SetProperty(ref _micStatusText, value); }
        public Color MicStatusColor { get => _micStatusColor; set => SetProperty(ref _micStatusColor, value); }

        public ObservableCollection<TrackInfo> Tracks { get; } = new();
        private TrackInfo? _selectedTrack;
        private bool _isTrackListVisible;
        private string _tracksSummary = "Доріжки";

        public bool IsTrackListVisible { get => _isTrackListVisible; set => SetProperty(ref _isTrackListVisible, value); }
        public string TracksSummary { get => _tracksSummary; set => SetProperty(ref _tracksSummary, value); }

        public int SongId
        {
            get => _songId;
            set { if (SetProperty(ref _songId, value)) _ = LoadAsync(value); }
        }

        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public string ArtistName { get => _artistName; set => SetProperty(ref _artistName, value); }
        public byte[]? AlbumCoverData { get => _albumCoverData; set => SetProperty(ref _albumCoverData, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public bool IsPlaying { get => _isPlaying; set => SetProperty(ref _isPlaying, value); }
        public bool IsMetronomeOn { get => _isMetronomeOn; set => SetProperty(ref _isMetronomeOn, value); }
        public bool IsSynthMuted { get => _isSynthMuted; set => SetProperty(ref _isSynthMuted, value); }
        public bool IsNoteCheckOn { get => _isNoteCheckOn; set => SetProperty(ref _isNoteCheckOn, value); }

        public double BpmMin { get => _bpmMin; set => SetProperty(ref _bpmMin, value); }
        public double BpmMax { get => _bpmMax; set => SetProperty(ref _bpmMax, value); }
        public double BpmValue
        {
            get => _bpmValue;
            set
            {
                if (SetProperty(ref _bpmValue, value))
                {
                    OnPropertyChanged(nameof(BpmLabel));
                    var factor = _originalBpm > 0 ? value / _originalBpm : 1.0;
                    _ = _bridge?.SetSpeedAsync(factor) ?? Task.CompletedTask;
                }
            }
        }
        public string BpmLabel => $"{Math.Round(_bpmValue)} BPM";

        public double TimelineValue
        {
            get => _timelineValue;
            set => SetProperty(ref _timelineValue, value);
        }
        public double TimelineMax { get => _timelineMax; set => SetProperty(ref _timelineMax, value); }
        public string TimeLabel { get => _timeLabel; set => SetProperty(ref _timeLabel, value); }

        public Color FeedbackColor { get => _feedbackColor; set => SetProperty(ref _feedbackColor, value); }

        public TrackInfo? SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                if (SetProperty(ref _selectedTrack, value) && value is not null && _engineReady)
                    _ = _bridge?.SetTrackAsync(value.Index) ?? Task.CompletedTask;
            }
        }

        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ToggleMetronomeCommand { get; }
        public ICommand ToggleSynthCommand { get; }
        public ICommand ToggleNoteCheckCommand { get; }
        public ICommand ToggleTrackListCommand { get; }
        public ICommand SelectTrackCommand { get; }
        public ICommand BackCommand { get; }

        public TabsPlayerViewModel()
        {
            PlayPauseCommand = new RelayCommand(async _ =>
            {
                if (_bridge is not null) await _bridge.PlayPauseAsync();
            });
            StopCommand = new RelayCommand(async _ =>
            {
                if (_bridge is not null) await _bridge.StopAsync();
            });
            ToggleMetronomeCommand = new RelayCommand(async _ =>
            {
                IsMetronomeOn = !IsMetronomeOn;
                if (_bridge is not null) await _bridge.SetMetronomeAsync(IsMetronomeOn);
            });
            ToggleSynthCommand = new RelayCommand(async _ =>
            {
                IsSynthMuted = !IsSynthMuted;
                if (_bridge is not null) await _bridge.SetMasterVolumeAsync(IsSynthMuted ? 0 : 1);
            });
            ToggleNoteCheckCommand = new RelayCommand(_ =>
            {
                ToggleNoteCheck();
                return Task.CompletedTask;
            });
            ToggleTrackListCommand = new RelayCommand(_ =>
            {
                IsTrackListVisible = !IsTrackListVisible;
                return Task.CompletedTask;
            });
            SelectTrackCommand = new RelayCommand(async o =>
            {
                if (o is TrackInfo track)
                {
                    SelectedTrack = track;
                    IsTrackListVisible = false;
                    if (_engineReady) await (_bridge?.SetTrackAsync(track.Index) ?? Task.CompletedTask);
                }
            });
            BackCommand = new RelayCommand(async _ =>
            {
                Teardown();
                await Shell.Current.GoToAsync("..");
            });
        }

        // Викликається з code-behind після створення WebView.
        public void AttachBridge(AlphaTabBridge bridge)
        {
            _bridge = bridge;
            _bridge.Ready += OnEngineReady;
            _bridge.ScoreLoaded += OnScoreLoaded;
            _bridge.PlayerStateChanged += OnPlayerStateChanged;
            _bridge.PositionChanged += OnPositionChanged;
            _bridge.ActiveNoteChanged += OnActiveNoteChanged;
            _bridge.ErrorOccurred += OnEngineError;
        }

        private void OnEngineReady()
        {
            _engineReady = true;
            if (_pendingTab is not null)
            {
                _ = _bridge?.LoadScoreAsync(_pendingTab);
                _pendingTab = null;
            }
        }

        private async Task LoadAsync(int songId)
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                var song = await _repo.GetSongByIdAsync(songId);
                if (song is null) { StatusMessage = "Пісню не знайдено."; return; }

                Title = song.Title;
                ArtistName = song.Artist?.Name ?? string.Empty;
                AlbumCoverData = song.AlbumCoverData;

                if (song.TabData is { Length: > 0 })
                {
                    // Якщо рушій уже готовий — вантажимо; інакше чекаємо OnEngineReady.
                    if (_engineReady) await (_bridge?.LoadScoreAsync(song.TabData) ?? Task.CompletedTask);
                    else _pendingTab = song.TabData;
                }
                else
                {
                    StatusMessage = "До цієї пісні не прикріплено файл табулатури.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Не вдалося завантажити пісню: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnScoreLoaded(ScoreInfo info)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _originalBpm = info.Tempo > 0 ? info.Tempo : 120;
                BpmMin = _originalBpm * 0.4;
                BpmMax = _originalBpm * 1.75;
                _bpmValue = _originalBpm;
                OnPropertyChanged(nameof(BpmValue));
                OnPropertyChanged(nameof(BpmLabel));

                Tracks.Clear();
                foreach (var t in info.Tracks)
                    Tracks.Add(t);

                // Активуємо першу доріжку з нотами (якщо є), інакше просто першу.
                var firstWithNotes = Tracks.FirstOrDefault(t => t.HasNotes) ?? Tracks.FirstOrDefault();
                SelectedTrack = firstWithNotes;
                if (firstWithNotes is not null && _engineReady)
                    _ = _bridge?.SetTrackAsync(firstWithNotes.Index);

                TracksSummary = Tracks.Count switch
                {
                    0 => "Немає доріжок",
                    1 => "1 доріжка",
                    _ => $"{Tracks.Count} доріжок"
                };

                StatusMessage = string.Empty;
            });
        }

        private void OnPlayerStateChanged(int state)
            => MainThread.BeginInvokeOnMainThread(() => IsPlaying = state == 1);

        private void OnPositionChanged(PlaybackPosition pos)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_isDraggingTimeline || pos.End <= 0) return;
                TimelineMax = pos.End;
                TimelineValue = pos.Current;

                var cur = TimeSpan.FromMilliseconds(pos.Current);
                var end = TimeSpan.FromMilliseconds(pos.End);
                TimeLabel = $"{cur:mm\\:ss} / {end:mm\\:ss}";
            });
        }

        private void OnActiveNoteChanged(int midi)
        {
            if (_isNoteCheckOn)
                _noteChecker?.SetExpectedMidi(midi);
        }

        private void OnEngineError(string code)
            => MainThread.BeginInvokeOnMainThread(() =>
                StatusMessage = "Помилка рушія табулатури. Перевірте підключення до інтернету.");

        // ---- Таймлайн (виклик з code-behind при перетягуванні) ----
        public void BeginTimelineDrag() => _isDraggingTimeline = true;
        public async void EndTimelineDrag()
        {
            _isDraggingTimeline = false;
            if (_bridge is not null) await _bridge.SeekAsync((int)TimelineValue);
        }

        // ---- Перевірка ноти через мікрофон ----
        private void ToggleNoteCheck()
        {
            if (_isNoteCheckOn)
            {
                _noteChecker?.Stop();
                IsNoteCheckOn = false;
                FeedbackColor = Colors.Transparent;
                MicNoteText = "--";
                MicStatusText = string.Empty;
                MicStatusColor = Colors.Transparent;
                return;
            }

            try
            {
                if (_noteChecker is null)
                {
                    var capture = AudioCaptureFactory.Create();
                    var tuner = new TunerService(capture);
                    _noteChecker = new NoteChecker(tuner);
                    _noteChecker.FeedbackChanged += OnMicFeedback;
                }
                _noteChecker.Start();
                IsNoteCheckOn = true;
                MicStatusText = "Слухаю…";
                MicStatusColor = Color.FromArgb("#8A2BE2");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Мікрофон недоступний: {ex.Message}";
            }
        }

        private void OnMicFeedback(MicFeedback fb)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!fb.IsListening)
                {
                    MicNoteText = "--";
                    MicStatusText = string.Empty;
                    MicStatusColor = Colors.Transparent;
                    FeedbackColor = Colors.Transparent;
                    return;
                }

                // Жива нота з мікрофона (видно завжди, коли є сигнал).
                MicNoteText = fb.HasSignal ? fb.Note : "--";

                // Текстовий статус + колір рамки залежно від стану перевірки.
                switch (fb.CheckState)
                {
                    case NoteCheckState.Correct:
                        MicStatusText = "Правильно!";
                        MicStatusColor = Color.FromArgb("#32CD32");
                        FeedbackColor = Color.FromRgba(50, 205, 50, 180);
                        break;
                    case NoteCheckState.Wrong:
                        MicStatusText = "Не та нота";
                        MicStatusColor = Color.FromArgb("#DC143C");
                        FeedbackColor = Color.FromRgba(220, 20, 60, 180);
                        break;
                    case NoteCheckState.Waiting:
                        MicStatusText = "Зіграйте ноту…";
                        MicStatusColor = Color.FromArgb("#FFA500");
                        FeedbackColor = Color.FromRgba(255, 165, 0, 120);
                        break;
                    default:
                        // Немає активної перевірки — показуємо просто, що слухаємо.
                        MicStatusText = fb.HasSignal ? $"Чути: {fb.Note}" : "Слухаю…";
                        MicStatusColor = fb.HasSignal ? Color.FromArgb("#8A2BE2") : Color.FromArgb("#808080");
                        FeedbackColor = fb.HasSignal
                            ? Color.FromRgba(138, 43, 226, 90)
                            : Colors.Transparent;
                        break;
                }
            });
        }

        private void Teardown()
        {
            try { _noteChecker?.Stop(); } catch { }
            _ = _bridge?.StopAsync();
        }
    }
}

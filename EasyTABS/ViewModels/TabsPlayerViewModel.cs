using System.Collections.ObjectModel;
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

        public ObservableCollection<TrackInfo> Tracks { get; } = new();
        private TrackInfo? _selectedTrack;

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

                SelectedTrack = Tracks.Count > 0 ? Tracks[0] : null;
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
                return;
            }

            try
            {
                if (_noteChecker is null)
                {
                    var capture = AudioCaptureFactory.Create();
                    var tuner = new TunerService(capture);
                    _noteChecker = new NoteChecker(tuner);
                    _noteChecker.StateChanged += OnNoteCheckState;
                }
                _noteChecker.Start();
                IsNoteCheckOn = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Мікрофон недоступний: {ex.Message}";
            }
        }

        private void OnNoteCheckState(NoteCheckState state)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FeedbackColor = state switch
                {
                    NoteCheckState.Correct => Color.FromRgba(50, 205, 50, 180),
                    NoteCheckState.Wrong => Color.FromRgba(220, 20, 60, 180),
                    NoteCheckState.Waiting => Color.FromRgba(255, 165, 0, 180),
                    _ => Colors.Transparent
                };
            });
        }

        private void Teardown()
        {
            try { _noteChecker?.Stop(); } catch { }
            _ = _bridge?.StopAsync();
        }
    }
}

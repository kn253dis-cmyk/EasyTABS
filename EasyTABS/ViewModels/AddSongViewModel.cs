using System.Collections.ObjectModel;
using System.Windows.Input;
using EasyTABS.Services;
using System.IO;

namespace EasyTABS.ViewModels
{
    public class AddSongViewModel : BaseViewModel
    {
        private readonly SongRepository _repo = new();

        private string _songName = string.Empty;
        private string _artist = string.Empty;
        private string _album = string.Empty;
        private string _selectedFilePath = string.Empty;
        private string _coverPath = string.Empty;
        private string _fileStatusText = "Натисніть, щоб обрати файл Guitar Pro";
        private bool _isFileSelected;
        private string _errorMessage = string.Empty;

        // Прапорці, щоб програмна підстановка тексту (після вибору підказки)
        // не відкривала попап знову.
        private bool _suppressSongFilter;
        private bool _suppressArtistFilter;
        private bool _suppressAlbumFilter;

        // Обкладинка, взята з обраного існуючого альбому (щоб не завантажувати знову).
        private byte[]? _selectedAlbumCover;
        private string _coverStatusText = string.Empty;

        public ObservableCollection<string> SongSuggestions { get; } = new();
        public ObservableCollection<string> ArtistSuggestions { get; } = new();
        public ObservableCollection<AlbumSuggestion> AlbumSuggestions { get; } = new();

        private bool _isAlbumSuggestionsVisible;
        public bool IsAlbumSuggestionsVisible
        {
            get => _isAlbumSuggestionsVisible;
            set => SetProperty(ref _isAlbumSuggestionsVisible, value);
        }

        public string CoverStatusText { get => _coverStatusText; set => SetProperty(ref _coverStatusText, value); }

        private bool _isSongSuggestionsVisible;
        public bool IsSongSuggestionsVisible
        {
            get => _isSongSuggestionsVisible;
            set => SetProperty(ref _isSongSuggestionsVisible, value);
        }

        private bool _isArtistSuggestionsVisible;
        public bool IsArtistSuggestionsVisible
        {
            get => _isArtistSuggestionsVisible;
            set => SetProperty(ref _isArtistSuggestionsVisible, value);
        }

        public string SongName
        {
            get => _songName;
            set { if (SetProperty(ref _songName, value) && !_suppressSongFilter) _ = FilterSongsAsync(); }
        }

        public string Artist
        {
            get => _artist;
            set { if (SetProperty(ref _artist, value) && !_suppressArtistFilter) _ = FilterArtistsAsync(); }
        }

        public string Album
        {
            get => _album;
            set
            {
                if (SetProperty(ref _album, value) && !_suppressAlbumFilter)
                {
                    // Ручна зміна назви альбому скидає прив'язку до існуючої обкладинки.
                    _selectedAlbumCover = null;
                    _ = FilterAlbumsAsync();
                }
            }
        }
        public string FileStatusText { get => _fileStatusText; set => SetProperty(ref _fileStatusText, value); }
        public bool IsFileSelected { get => _isFileSelected; set => SetProperty(ref _isFileSelected, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        public ICommand PickFileCommand { get; }
        public ICommand PickCoverCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand SelectSongSuggestionCommand { get; }
        public ICommand SelectArtistSuggestionCommand { get; }
        public ICommand SelectAlbumSuggestionCommand { get; }

        public AddSongViewModel()
        {
            PickFileCommand = new RelayCommand(async _ => await PickFileAsync());
            PickCoverCommand = new RelayCommand(async _ => await PickCoverAsync());
            AddCommand = new RelayCommand(async _ => await AddAsync());

            SelectSongSuggestionCommand = new RelayCommand(o =>
            {
                if (o is string title)
                {
                    _suppressSongFilter = true;
                    SongName = title;
                    _suppressSongFilter = false;
                    IsSongSuggestionsVisible = false;
                }
                return Task.CompletedTask;
            });

            SelectArtistSuggestionCommand = new RelayCommand(o =>
            {
                if (o is string name)
                {
                    _suppressArtistFilter = true;
                    Artist = name;
                    _suppressArtistFilter = false;
                    IsArtistSuggestionsVisible = false;
                }
                return Task.CompletedTask;
            });

            SelectAlbumSuggestionCommand = new RelayCommand(o =>
            {
                if (o is AlbumSuggestion album)
                {
                    _suppressAlbumFilter = true;
                    Album = album.Title;
                    _suppressAlbumFilter = false;
                    IsAlbumSuggestionsVisible = false;

                    // Використовуємо наявну обкладинку альбому — завантажувати не треба.
                    _selectedAlbumCover = album.CoverData;
                    if (album.CoverData is { Length: > 0 })
                    {
                        CoverStatusText = $"Обкладинку взято з альбому «{album.Title}»";
                        _coverPath = string.Empty; // скидаємо ручний файл
                    }

                    // Якщо артист порожній — підставляємо артиста альбому.
                    if (string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(album.ArtistName))
                    {
                        _suppressArtistFilter = true;
                        Artist = album.ArtistName;
                        _suppressArtistFilter = false;
                    }
                }
                return Task.CompletedTask;
            });
        }

        private async Task FilterSongsAsync()
        {
            var query = SongName?.Trim() ?? string.Empty;
            SongSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                IsSongSuggestionsVisible = false;
                return;
            }

            try
            {
                var titles = await _repo.GetSongTitlesAsync(query);
                foreach (var t in titles)
                    SongSuggestions.Add(t);
            }
            catch { }

            IsSongSuggestionsVisible = SongSuggestions.Count > 0;
        }

        private async Task FilterArtistsAsync()
        {
            var query = Artist?.Trim() ?? string.Empty;
            ArtistSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                IsArtistSuggestionsVisible = false;
                return;
            }

            try
            {
                var names = await _repo.GetArtistNamesAsync(query);
                foreach (var n in names)
                    ArtistSuggestions.Add(n);
            }
            catch { }

            IsArtistSuggestionsVisible = ArtistSuggestions.Count > 0;
        }

        private async Task FilterAlbumsAsync()
        {
            var query = Album?.Trim() ?? string.Empty;
            AlbumSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                IsAlbumSuggestionsVisible = false;
                CoverStatusText = string.Empty;
                return;
            }

            try
            {
                // Якщо вказано артиста — фільтруємо альбоми тільки його.
                var artist = string.IsNullOrWhiteSpace(Artist) ? null : Artist.Trim();
                var albums = await _repo.GetAlbumSuggestionsAsync(query, artist);
                foreach (var a in albums)
                    AlbumSuggestions.Add(a);
            }
            catch { }

            IsAlbumSuggestionsVisible = AlbumSuggestions.Count > 0;
        }

        private async Task PickFileAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Оберіть файл Guitar Pro"
                });

                if (result is not null)
                {
                    _selectedFilePath = result.FullPath;
                    FileStatusText = result.FileName;
                    IsFileSelected = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не вдалося обрати файл: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"PickFile FULL: {ex}");
            }
        }

        private async Task PickCoverAsync()
        {
            try
            {
                var imageTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg" } },
                { DevicePlatform.Android, new[] { "image/png", "image/jpeg" } },
                { DevicePlatform.iOS, new[] { "public.image" } },
                { DevicePlatform.MacCatalyst, new[] { "public.image" } },
                    });

                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Оберіть обкладинку",
                    FileTypes = imageTypes
                });

                if (result is not null)
                    _coverPath = result.FullPath;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не вдалося обрати обкладинку: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"PickCover FULL: {ex}");
            }
        }

        private async Task AddAsync()
        {
            ErrorMessage = string.Empty;
            IsSongSuggestionsVisible = false;
            IsArtistSuggestionsVisible = false;
            IsAlbumSuggestionsVisible = false;

            if (string.IsNullOrWhiteSpace(SongName) || string.IsNullOrWhiteSpace(Artist))
            {
                ErrorMessage = "Вкажіть назву пісні та виконавця";
                return;
            }

            try
            {
                byte[]? tabBytes = null;
                string tabFileName = string.Empty;
                if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
                {
                    tabBytes = await File.ReadAllBytesAsync(_selectedFilePath);
                    tabFileName = Path.GetFileName(_selectedFilePath);
                }

                // Обкладинка: пріоритет — обрана з існуючого альбому,
                // інакше вручну завантажений файл.
                byte[]? coverBytes = _selectedAlbumCover;
                if (coverBytes is null &&
                    !string.IsNullOrEmpty(_coverPath) && File.Exists(_coverPath))
                    coverBytes = await File.ReadAllBytesAsync(_coverPath);

                await _repo.AddSongAsync(SongName.Trim(), Artist.Trim(), Album?.Trim() ?? "",
                                         tabBytes, tabFileName, coverBytes);

                await Shell.Current.DisplayAlertAsync("Додано", $"\"{SongName}\" у бібліотеці", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (InvalidOperationException dup)
            {
                ErrorMessage = dup.Message;
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException is not null) inner = inner.InnerException;
                ErrorMessage = $"Не вдалося зберегти: {inner.Message}";
            }
        }
    }
}

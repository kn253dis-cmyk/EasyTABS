using System.Collections.ObjectModel;
using System.Windows.Input;
using EasyTABS.Models;

namespace EasyTABS.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        // Повний список (джерело даних).
        private readonly List<Song> _allSongs = new();

        private string _searchText = string.Empty;
        private Song? _selectedSong;

        public ObservableCollection<Song> Songs { get; } = new();
        public ObservableCollection<Song> Suggestions { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        private bool _isSuggestionsVisible;
        public bool IsSuggestionsVisible
        {
            get => _isSuggestionsVisible;
            set => SetProperty(ref _isSuggestionsVisible, value);
        }

        public Song? SelectedSong
        {
            get => _selectedSong;
            set
            {
                if (SetProperty(ref _selectedSong, value) && value is not null)
                    _ = OpenSongAsync(value);
            }
        }

        public ICommand OpenTunerCommand { get; }
        public ICommand AddSongCommand { get; }
        public ICommand SearchCommand { get; }

        public MainViewModel()
        {
            OpenTunerCommand = new RelayCommand(async _ =>
                await Shell.Current.GoToAsync("TunerPage"));
            AddSongCommand = new RelayCommand(async _ =>
                await Shell.Current.GoToAsync("AddSongPage"));
            SearchCommand = new RelayCommand(_ => ApplyFilter());

            LoadSampleData();
        }

        private void LoadSampleData()
        {
            // Тимчасові дані замість бази. Замініть на завантаження з сервісу/БД.
            _allSongs.AddRange(new[]
            {
                new Song { Id = 1, Title = "Nothing Else Matters", Artist=new Artist("Metallica"), Album = "Metallica" },
                new Song { Id = 2, Title = "Wish You Were Here", Artist=new Artist("Pink Floyd"), Album = "Wish You Were Here" },
                new Song { Id = 3, Title = "Hotel California", Artist=new Artist("Eagles"), Album = "Hotel California" },
                new Song { Id = 4, Title = "Stairway to Heaven", Artist=new Artist("Led Zeppelin"), Album = "Led Zeppelin IV" },
            });

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var query = SearchText?.Trim() ?? string.Empty;

            IEnumerable<Song> filtered = string.IsNullOrEmpty(query)
                ? _allSongs
                : _allSongs.Where(s =>
                    s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.Artist.ToString()
                            .Contains(query, StringComparison.OrdinalIgnoreCase));

            Songs.Clear();
            foreach (var song in filtered)
                Songs.Add(song);

            // Підказки показуємо лише коли є текст пошуку.
            Suggestions.Clear();
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var song in filtered.Take(5))
                    Suggestions.Add(song);
            }
            IsSuggestionsVisible = Suggestions.Count > 0;
        }

        private async Task OpenSongAsync(Song song)
        {
            // TODO: відкриття табулатури обраної пісні.
            await Shell.Current.DisplayAlertAsync(song.Title, $"Виконавець: {song.Artist}", "OK");
            SelectedSong = null; // скидаємо вибір
        }
    }
}

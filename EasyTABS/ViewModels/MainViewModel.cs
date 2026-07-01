using System.Collections.ObjectModel;
using System.Windows.Input;
using EasyTABS.Models;
using EasyTABS.Services;

namespace EasyTABS.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly SongRepository _repo = new();

        private string _searchText = string.Empty;
        private Song? _selectedSong;

        // Прапорець, щоб програмна зміна SearchText (після вибору пісні)
        // не відкривала попап знову.
        private bool _suppressFilter;

        public ObservableCollection<Song> Songs { get; } = new();
        public ObservableCollection<Song> Suggestions { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value) && !_suppressFilter) _ = ApplyFilterAsync(); }
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
            set { if (SetProperty(ref _selectedSong, value) && value is not null) _ = OpenSongAsync(value); }
        }

        public ICommand OpenTunerCommand { get; }
        public ICommand AddSongCommand { get; }
        public ICommand SearchCommand { get; }

        public MainViewModel()
        {
            OpenTunerCommand = new RelayCommand(async _ => await Shell.Current.GoToAsync("TunerPage"));
            AddSongCommand = new RelayCommand(async _ => await Shell.Current.GoToAsync("AddSongPage"));
            SearchCommand = new RelayCommand(async _ =>
            {
                // Явний пошук ховає попап — користувач уже "підтвердив" запит.
                await ApplyFilterAsync();
                IsSuggestionsVisible = false;
            });
        }

        // Оновлення списку при поверненні на сторінку (нова пісня могла додатись).
        public async Task RefreshAsync() => await ApplyFilterAsync();

        private async Task ApplyFilterAsync()
        {
            var query = SearchText?.Trim() ?? string.Empty;

            List<Song> filtered;
            try
            {
                filtered = await _repo.SearchAsync(query);
            }
            catch
            {
                filtered = new List<Song>();
            }

            Songs.Clear();
            foreach (var song in filtered)
                Songs.Add(song);

            Suggestions.Clear();

            // Попап показуємо ЛИШЕ коли є непорожній запит І знайдено збіги.
            // Порожнє поле -> попап гарантовано ховається.
            if (string.IsNullOrWhiteSpace(query))
            {
                IsSuggestionsVisible = false;
                return;
            }

            foreach (var song in filtered.Take(5))
                Suggestions.Add(song);

            IsSuggestionsVisible = Suggestions.Count > 0;
        }

        private async Task OpenSongAsync(Song song)
        {
            // Ховаємо попап і скидаємо виділення перед навігацією.
            IsSuggestionsVisible = false;

            _suppressFilter = true;
            SearchText = song.Title;
            _suppressFilter = false;

            var id = song.Id;
            SelectedSong = null;

            await Shell.Current.GoToAsync($"TabsPlayerPage?songId={id}");
        }
    }
}

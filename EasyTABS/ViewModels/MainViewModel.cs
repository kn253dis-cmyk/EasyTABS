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

        public ObservableCollection<Song> Songs { get; } = new();
        public ObservableCollection<Song> Suggestions { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _ = ApplyFilterAsync(); }
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
            SearchCommand = new RelayCommand(async _ => await ApplyFilterAsync());

            _ = ApplyFilterAsync();
        }

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
            if (!string.IsNullOrEmpty(query))
                foreach (var song in filtered.Take(5))
                    Suggestions.Add(song);

            IsSuggestionsVisible = Suggestions.Count > 0;
        }

        private async Task OpenSongAsync(Song song)
        {
            await Shell.Current.DisplayAlertAsync(song.Title, $"Виконавець: {song.Artist?.Name}", "OK");
            SelectedSong = null;
        }
    }
}
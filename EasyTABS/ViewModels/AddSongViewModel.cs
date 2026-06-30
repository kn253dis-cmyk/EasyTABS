using System.Windows.Input;
using EasyTABS.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace EasyTABS.ViewModels
{
    public class AddSongViewModel : BaseViewModel
    {
        private string _songName = string.Empty;
        private string _artist = string.Empty;
        private string _album = string.Empty;
        private string _selectedFilePath = string.Empty;
        private string _fileStatusText = "Натисніть, щоб обрати файл Guitar Pro";
        private bool _isFileSelected;
        private string _errorMessage = string.Empty;

        public string SongName
        {
            get => _songName;
            set => SetProperty(ref _songName, value);
        }

        public string Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, value);
        }

        public string Album
        {
            get => _album;
            set => SetProperty(ref _album, value);
        }

        public string FileStatusText
        {
            get => _fileStatusText;
            set => SetProperty(ref _fileStatusText, value);
        }

        public bool IsFileSelected
        {
            get => _isFileSelected;
            set => SetProperty(ref _isFileSelected, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand PickFileCommand { get; }
        public ICommand AddCommand { get; }

        public AddSongViewModel()
        {
            PickFileCommand = new RelayCommand(async _ => await PickFileAsync());
            AddCommand = new RelayCommand(async _ => await AddAsync());
        }

        private async Task PickFileAsync()
        {
            try
            {
                // Drag-and-drop із WPF на мобільних не працює — замінюємо вибором файлу.
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
            }
        }

        private async Task AddAsync()
        {
            ErrorMessage = string.Empty;

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

                using var db = new EasyTABS.Data.Database();

                // Знаходимо існуючого виконавця або створюємо нового,
                // щоб не плодити дублікати Artist.
                var artist = await db.Artists
                    .FirstOrDefaultAsync(a => a.Name == Artist)
                    ?? new Artist { Name = Artist };

                var song = new Song
                {
                    Title = SongName,
                    Album = Album,
                    Artist = artist,
                    TabData = tabBytes,
                    TabFileName = tabFileName
                };

                db.Songs.Add(song);
                await db.SaveChangesAsync();

                await Shell.Current.DisplayAlertAsync("Додано", $"\"{song.Title}\" у бібліотеці", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не вдалося зберегти: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"AddSong error: {ex}");
            }
        }
    }
}

using System.Windows.Input;
using EasyTABS.Models;

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

            var song = new Song
            {
                Title = SongName,
                Album = Album,
                Artist = new Artist { Name = Artist },
                FilePath = _selectedFilePath
            };
            // TODO: збереження пісні через сервіс/БД.
            await Shell.Current.DisplayAlertAsync("Додано", $"\"{song.Title}\" у бібліотеці", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }
}

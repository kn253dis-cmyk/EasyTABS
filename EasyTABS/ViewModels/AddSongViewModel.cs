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

        public string SongName { get => _songName; set => SetProperty(ref _songName, value); }
        public string Artist { get => _artist; set => SetProperty(ref _artist, value); }
        public string Album { get => _album; set => SetProperty(ref _album, value); }
        public string FileStatusText { get => _fileStatusText; set => SetProperty(ref _fileStatusText, value); }
        public bool IsFileSelected { get => _isFileSelected; set => SetProperty(ref _isFileSelected, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        public ICommand PickFileCommand { get; }
        public ICommand PickCoverCommand { get; }
        public ICommand AddCommand { get; }

        public AddSongViewModel()
        {
            PickFileCommand = new RelayCommand(async _ => await PickFileAsync());
            PickCoverCommand = new RelayCommand(async _ => await PickCoverAsync());
            AddCommand = new RelayCommand(async _ => await AddAsync());
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
                System.Diagnostics.Debug.WriteLine($"PickCover FULL: {ex}");
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

                byte[]? coverBytes = null;
                if (!string.IsNullOrEmpty(_coverPath) && File.Exists(_coverPath))
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
                //System.Diagnostics.Debug.WriteLine($"AddSong FULL: {ex}");
            }
        }
    }
}
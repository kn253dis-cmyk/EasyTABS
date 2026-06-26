using System.Windows.Input;

namespace EasyTABS.ViewModels
{
    public class TunerViewModel : BaseViewModel
    {
        private string _noteText = "--";
        private string _frequencyText = "0.00 Hz";
        private bool _isListening;

        public string NoteText
        {
            get => _noteText;
            set => SetProperty(ref _noteText, value);
        }

        public string FrequencyText
        {
            get => _frequencyText;
            set => SetProperty(ref _frequencyText, value);
        }

        public bool IsListening
        {
            get => _isListening;
            set => SetProperty(ref _isListening, value);
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        public TunerViewModel()
        {
            StartCommand = new RelayCommand(_ => Start());
            StopCommand = new RelayCommand(_ => Stop());
        }

        private void Start()
        {
            // TODO: захоплення мікрофона + FFT для визначення частоти.
            // Поки що — заглушка UI без обробки аудіо.
            IsListening = true;
            NoteText = "A";
            FrequencyText = "440.00 Hz";
        }

        private void Stop()
        {
            IsListening = false;
            NoteText = "--";
            FrequencyText = "0.00 Hz";
        }
    }
}

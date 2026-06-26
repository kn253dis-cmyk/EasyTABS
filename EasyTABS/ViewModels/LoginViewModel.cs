using System.Windows.Input;

namespace EasyTABS.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private string _login = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;

        public string Login
        {
            get => _login;
            set => SetProperty(ref _login, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand GoToRegisterCommand { get; }
        public ICommand ForgotPasswordCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async _ => await DoLoginAsync());
            GoToRegisterCommand = new RelayCommand(async _ =>
                await Shell.Current.GoToAsync("RegisterPage"));
            ForgotPasswordCommand = new RelayCommand(async _ =>
                await Shell.Current.DisplayAlert("Відновлення", "Функція ще в розробці", "OK"));
        }

        private async Task DoLoginAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Введіть логін і пароль";
                return;
            }

            // TODO: тут має бути виклик сервісу авторизації.
            // Поки що — перехід на головну сторінку.
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}

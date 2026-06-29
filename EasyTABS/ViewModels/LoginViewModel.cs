using EasyTABS.Entity;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

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
                await Shell.Current.DisplayAlertAsync("Відновлення", "Функція ще в розробці", "OK"));
        }

        private async Task DoLoginAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Введіть логін і пароль";
                return;
            }

            try
            {
                using (var db = new EasyTABS.Data.Database())
                {
                    string hashPassword = db.HashPassword(Password);

                    // Використовуємо FirstOrDefaultAsync, перевіряємо і Email, і NickName
                    var user = await db.Users.FirstOrDefaultAsync(u =>
                        (u.Email == Login || u.NickName == Login) && u.Password == hashPassword);

                    if (user != null)
                        await Shell.Current.GoToAsync("MainPage");
                    else
                        // Якщо дані неправильні
                        ErrorMessage = "Невірний логін або пароль";
                }
            }
            catch (Exception ex)
            {
                // Виводимо реальну помилку підключення на екран для дебагу
                ErrorMessage = $"Помилка БД: {ex.InnerException?.Message ?? ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Login error: {ex}");
            }
        }
    }
}

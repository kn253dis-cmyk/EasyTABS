using EasyTABS.Entity;
using System.Windows.Input;

namespace EasyTABS.ViewModels
{
    public class RegisterViewModel : BaseViewModel
    {
        private string _nickname = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private bool _agreedToTerms;
        private string _errorMessage = string.Empty;

        public string Nickname
        {
            get => _nickname;
            set => SetProperty(ref _nickname, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public bool AgreedToTerms
        {
            get => _agreedToTerms;
            set => SetProperty(ref _agreedToTerms, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand RegisterCommand { get; }
        public ICommand GoToLoginCommand { get; }

        public RegisterViewModel()
        {
            RegisterCommand = new RelayCommand(async _ => await DoRegisterAsync());
            GoToLoginCommand = new RelayCommand(async _ => await Shell.Current.GoToAsync(".."));
        }

        private async Task DoRegisterAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Nickname) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Заповніть усі поля";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Паролі не співпадають";
                return;
            }

            if (!AgreedToTerms)
            {
                ErrorMessage = "Потрібно прийняти умови використання";
                return;
            }

            try
            {
                using var db = new EasyTABS.Data.Database();
                //await db.Database.EnsureCreatedAsync();


                // Перевірка, чи email/нікнейм уже зайняті
                bool exists = db.Users.Any(u => u.Email == Email || u.NickName == Nickname);
                if (exists)
                {
                    ErrorMessage = "Користувач з таким email або нікнеймом вже існує";
                    return;
                }

                string hashedPassword = db.HashPassword(Password);
                var newUser = new User
                {
                    Password = hashedPassword,
                    Email = Email,
                    NickName = Nickname
                };

                db.Add(newUser);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Не вдалося створити акаунт. Спробуйте ще раз.";
                System.Diagnostics.Debug.WriteLine($"Register error: {ex}");
                return;
            }

            await Shell.Current.DisplayAlertAsync("Готово", "Акаунт створено", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }
}

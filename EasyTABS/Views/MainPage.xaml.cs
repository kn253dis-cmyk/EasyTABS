using EasyTABS.ViewModels;

namespace EasyTABS.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Оновлюємо список при поверненні (напр. після додавання пісні).
            if (BindingContext is MainViewModel vm)
                await vm.RefreshAsync();
        }
    }
}

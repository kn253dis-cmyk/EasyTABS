using EasyTABS.ViewModels;
using MauiIcons.Material;

namespace EasyTABS.Views
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            _ = new MauiIcons.Core.MauiIcon(); // обхід бага MAUI з URL-namespace
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

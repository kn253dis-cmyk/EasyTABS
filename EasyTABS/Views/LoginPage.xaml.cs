using MauiIcons.Material;

namespace EasyTABS.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();
            _ = new MauiIcons.Core.MauiIcon(); // обхід бага MAUI з URL-namespace
        }
    }
}

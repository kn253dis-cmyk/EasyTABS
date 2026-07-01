using MauiIcons.Material;

namespace EasyTABS.Views
{
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage()
        {
            InitializeComponent();
            _ = new MauiIcons.Core.MauiIcon(); // обхід бага MAUI з URL-namespace
        }
    }
}

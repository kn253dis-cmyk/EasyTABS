using EasyTABS.Services;
using EasyTABS.ViewModels;

namespace EasyTABS.Views
{
    public partial class TabsPlayerPage : ContentPage
    {
        private AlphaTabBridge? _bridge;

        public TabsPlayerPage()
        {
            InitializeComponent();
            InitWebView();
        }

        private async void InitWebView()
        {
            // Піднімаємо міст і прив'язуємо його до ViewModel.
            _bridge = new AlphaTabBridge(TabWebView);
            if (BindingContext is TabsPlayerViewModel vm)
                vm.AttachBridge(_bridge);

            // Завантажуємо HTML-хост із Resources/Raw у WebView.
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("alphatab_host.html");
                using var reader = new StreamReader(stream);
                var html = await reader.ReadToEndAsync();
                TabWebView.Source = new HtmlWebViewSource { Html = html };
            }
            catch (Exception ex)
            {
                if (BindingContext is TabsPlayerViewModel vmErr)
                    vmErr.StatusMessage = $"Не вдалося завантажити плеєр: {ex.Message}";
            }
        }

        // Перехоплюємо hash-навігацію "#athost=..." як міст JS -> C#.
        private void TabWebView_Navigating(object? sender, WebNavigatingEventArgs e)
        {
            if (_bridge is not null && _bridge.TryHandleNavigation(e.Url))
                e.Cancel = true;
        }

        private void Timeline_DragStarted(object? sender, EventArgs e)
        {
            if (BindingContext is TabsPlayerViewModel vm) vm.BeginTimelineDrag();
        }

        private void Timeline_DragCompleted(object? sender, EventArgs e)
        {
            if (BindingContext is TabsPlayerViewModel vm) vm.EndTimelineDrag();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Зупиняємо звук/мікрофон при виході.
            if (BindingContext is TabsPlayerViewModel vm)
                vm.StopCommand.Execute(null);
        }
    }
}

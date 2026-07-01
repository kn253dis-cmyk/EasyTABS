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
            _ = new MauiIcons.Core.MauiIcon(); // обхід бага MAUI з URL-namespace

            // На Windows підключаємо нативний канал WebView2 postMessage.
            TabWebView.HandlerChanged += OnWebViewHandlerChanged;

            InitWebView();
        }

        private async void InitWebView()
        {
            _bridge = new AlphaTabBridge(TabWebView);
            if (BindingContext is TabsPlayerViewModel vm)
                vm.AttachBridge(_bridge);

            try
            {
                // Читаємо HTML-хост із пакета.
                using var stream = await FileSystem.OpenAppPackageFileAsync("alphatab_host.html");
                using var reader = new StreamReader(stream);
                var html = await reader.ReadToEndAsync();

                // Пишемо у кеш і вантажимо через file:// — це дає документу
                // нормальний origin, тож CDN-скрипти AlphaTab коректно
                // завантажуються і на Windows WebView2, і на Android.
                var path = Path.Combine(FileSystem.CacheDirectory, "alphatab_host.html");
                await File.WriteAllTextAsync(path, html);

                TabWebView.Source = new UrlWebViewSource { Url = new Uri(path).AbsoluteUri };
            }
            catch (Exception ex)
            {
                if (BindingContext is TabsPlayerViewModel vmErr)
                    vmErr.StatusMessage = $"Не вдалося завантажити плеєр: {ex.Message}";
            }
        }

        // Windows-специфічний канал: WebView2.WebMessageReceived.
        private void OnWebViewHandlerChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            if (TabWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 native)
            {
                native.WebMessageReceived -= OnWindowsWebMessage;
                native.WebMessageReceived += OnWindowsWebMessage;
            }
#endif
        }

#if WINDOWS
        private void OnWindowsWebMessage(
            Microsoft.UI.Xaml.Controls.WebView2 sender,
            Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
        {
            string? msg = null;
            try { msg = args.TryGetWebMessageAsString(); } catch { }
            if (string.IsNullOrEmpty(msg)) return;

            // Диспетчеримо на UI-потік і віддаємо мосту як звичайне повідомлення.
            MainThread.BeginInvokeOnMainThread(() => _bridge?.DispatchMessage(msg));
        }
#endif

        // Перехоплюємо навігацію "athost://..." як міст JS -> C# (Android/iOS).
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
            if (BindingContext is TabsPlayerViewModel vm)
                vm.StopCommand.Execute(null);
        }
    }
}

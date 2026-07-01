using EasyTABS.Views;

namespace EasyTABS
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Реєстрація маршрутів для навігації GoToAsync.
            Routing.RegisterRoute("RegisterPage", typeof(RegisterPage));
            Routing.RegisterRoute("MainPage", typeof(MainPage));
            Routing.RegisterRoute("AddSongPage", typeof(AddSongPage));
            Routing.RegisterRoute("TunerPage", typeof(TunerPage));
            Routing.RegisterRoute("TabsPlayerPage", typeof(TabsPlayerPage));
        }
    }
}

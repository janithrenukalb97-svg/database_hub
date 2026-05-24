using Prism.Unity;
using Prism.Ioc;
using Database_Hub.Views;
using Database_Hub.Views.ConnectedMode;
using Database_Hub.ViewModels;
using Database_Hub.ViewModels.ConnectedMode;
using System.Windows;

namespace Database_Hub
{
    internal class Bootstrapper : PrismBootstrapper
    {
        protected override DependencyObject CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Register SessionService as singleton
            containerRegistry.RegisterSingleton<Database_Hub.Services.SessionService, Database_Hub.Services.SessionService>();
            // Authentication Views
            containerRegistry.RegisterForNavigation<LoginView, LoginViewModel>();
            containerRegistry.RegisterForNavigation<DatabaseConnectionView, DatabaseConnectionViewModel>();

            // Guest Mode Views
            // containerRegistry.RegisterForNavigation<GuestHomeView, GuestHomeViewModel>("GuestHomeView");

            // Connected Mode Views
            containerRegistry.RegisterForNavigation<ConnectedHomeView, ConnectedHomeViewModel>("ConnectedHomeView");
            containerRegistry.RegisterForNavigation<Database_Hub.Views.ConnectedMode.Pages.MainFunctionHomeView, Database_Hub.ViewModels.ConnectedMode.MainFunctionHomeViewModel>("MainFunctionHomeView");
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            var regionManager = Container.Resolve<Prism.Regions.IRegionManager>();
            regionManager.RequestNavigate("MainRegion", "LoginView");
        }
    }
}
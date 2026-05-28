using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class ConnectedHomeViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private string _welcomeMessage = string.Empty;
        private string _serverName = string.Empty;
        private readonly Database_Hub.Services.SessionService _sessionService;

        public string WelcomeMessage
        {
            get => _welcomeMessage;
            set => SetProperty(ref _welcomeMessage, value);
        }

        public string ServerName
        {
            get => _serverName;
            set => SetProperty(ref _serverName, value);
        }

        public DelegateCommand LogoutCommand { get; private set; }
        public DelegateCommand NavigateMainFunctionCommand { get; private set; }

        public ConnectedHomeViewModel(IRegionManager regionManager, Database_Hub.Services.SessionService sessionService)
        {
            _regionManager = regionManager;
            _sessionService = sessionService;
            WelcomeMessage = "You are now authenticated and connected to the database. All features are available.";
            LogoutCommand = new DelegateCommand(Logout);
            NavigateMainFunctionCommand = new DelegateCommand(NavigateToMainFunctionHome);
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // Receive the server name from navigation parameters
            if (navigationContext.Parameters.ContainsKey("serverName"))
            {
                var serverName = navigationContext.Parameters.GetValue<string>("serverName");
                ServerName = $"Server: {serverName}";
                _sessionService.CurrentServerName = ServerName;
                _sessionService.CurrentServerAddress = serverName;
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // Called when navigating away from this view
        }

        private void Logout()
        {
            _sessionService.CurrentServerName = string.Empty;
            _sessionService.CurrentServerAddress = string.Empty;
            _regionManager.RequestNavigate("MainRegion", "LoginView");
        }

        private void NavigateToMainFunctionHome()
        {
            _regionManager.RequestNavigate("MainRegion", "MainFunctionHomeView");
        }
    }
}
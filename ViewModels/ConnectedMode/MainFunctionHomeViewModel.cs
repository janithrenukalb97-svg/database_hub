using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class MainFunctionHomeViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly Database_Hub.Services.SessionService _sessionService;
        private string _serverName;

        public string ServerName
        {
            get => _serverName;
            set => SetProperty(ref _serverName, value);
        }

        public DelegateCommand LogoutCommand { get; private set; }

        public MainFunctionHomeViewModel(Database_Hub.Services.SessionService sessionService, IRegionManager regionManager)
        {
            _sessionService = sessionService;
            _regionManager = regionManager;
            ServerName = _sessionService.CurrentServerName;
            LogoutCommand = new DelegateCommand(Logout);
        }

        private void Logout()
        {
            _regionManager.RequestNavigate("MainRegion", "LoginView");
        }
    }
}

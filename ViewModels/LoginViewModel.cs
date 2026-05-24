using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace Database_Hub.ViewModels
{
    public class LoginViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;

        public DelegateCommand ConnectCommand { get; private set; }
        public DelegateCommand GuestCommand { get; private set; }

        public LoginViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;
            ConnectCommand = new DelegateCommand(NavigateToDatabaseConnection);
            GuestCommand = new DelegateCommand(NavigateToHomeAsGuest);
        }

        private void NavigateToDatabaseConnection()
        {
            _regionManager.RequestNavigate("MainRegion", "DatabaseConnectionView");
        }

        private void NavigateToHomeAsGuest()
        {
            _regionManager.RequestNavigate("MainRegion", "GuestHomeView");
        }
    }
}
namespace Database_Hub.Services
{
    public class SessionService : Prism.Mvvm.BindableBase
    {
        private string _currentServerName = string.Empty;
        private string _currentServerAddress = string.Empty;

        public string CurrentServerName
        {
            get => _currentServerName;
            set => SetProperty(ref _currentServerName, value);
        }

        public string CurrentServerAddress
        {
            get => _currentServerAddress;
            set => SetProperty(ref _currentServerAddress, value);
        }
    }
}

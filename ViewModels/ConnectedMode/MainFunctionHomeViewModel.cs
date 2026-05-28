using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class MainFunctionHomeViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly Database_Hub.Services.SessionService _sessionService;
        private string _serverName = string.Empty;
        private string _selectedMenuItem = "File Explorer";
        private bool _isNavCollapsed;

        public string ServerName
        {
            get => _serverName;
            set => SetProperty(ref _serverName, value);
        }

        public string SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value))
                {
                    RaisePropertyChanged(nameof(IsObjectExplorerSelected));
                    RaisePropertyChanged(nameof(IsReleaseExecutorSelected));
                    RaisePropertyChanged(nameof(IsFileExplorerSelected));
                    RaisePropertyChanged(nameof(IsComparisonToolSelected));

                    if (value == "Object Explorer")
                    {
                        _ = ObjectExplorerViewModel.LoadDatabasesAsync();
                    }
                }
            }
        }

        public bool IsNavCollapsed
        {
            get => _isNavCollapsed;
            set
            {
                if (SetProperty(ref _isNavCollapsed, value))
                {
                    RaisePropertyChanged(nameof(ToggleNavIcon));
                    RaisePropertyChanged(nameof(ObjectExplorerNavContent));
                    RaisePropertyChanged(nameof(ReleaseExecutorNavContent));
                    RaisePropertyChanged(nameof(FileExplorerNavContent));
                    RaisePropertyChanged(nameof(ComparisonToolNavContent));
                    RaisePropertyChanged(nameof(LogoutNavContent));
                }
            }
        }

        public bool IsObjectExplorerSelected => SelectedMenuItem == "Object Explorer";
        public bool IsReleaseExecutorSelected => SelectedMenuItem == "Release Executor";
        public bool IsFileExplorerSelected => SelectedMenuItem == "File Explorer";
        public bool IsComparisonToolSelected => SelectedMenuItem == "Comparison Tool";

        public string ToggleNavIcon => "☰";
        public string ObjectExplorerNavContent => IsNavCollapsed ? "◎" : "◎  Object Explorer";
        public string ReleaseExecutorNavContent => IsNavCollapsed ? "⇅" : "⇅  Release Executor";
        public string FileExplorerNavContent => IsNavCollapsed ? "▦" : "▦  File Explorer";
        public string ComparisonToolNavContent => IsNavCollapsed ? "≍" : "≍  Comparison Tool";
        public string LogoutNavContent => IsNavCollapsed ? "⎋" : "⎋  Logout";

        public ObjectExplorerViewModel ObjectExplorerViewModel { get; }
        public ReleaseExecutorViewModel ReleaseExecutorViewModel { get; }
        public FileExplorerViewModel FileExplorerViewModel { get; }
        public ComparisonToolViewModel ComparisonToolViewModel { get; }

        public DelegateCommand LogoutCommand { get; }
        public DelegateCommand ToggleNavCommand { get; }
        public DelegateCommand<string> NavigateMenuCommand { get; }

        public MainFunctionHomeViewModel(
            Database_Hub.Services.SessionService sessionService,
            IRegionManager regionManager,
            Database_Hub.Services.ActionLoggerService actionLogger)
        {
            _sessionService = sessionService;
            _regionManager = regionManager;
            ServerName = _sessionService.CurrentServerName;

            ObjectExplorerViewModel = new ObjectExplorerViewModel(_sessionService, actionLogger);
            ReleaseExecutorViewModel = new ReleaseExecutorViewModel(actionLogger, _sessionService);
            FileExplorerViewModel = new FileExplorerViewModel(actionLogger);
            ComparisonToolViewModel = new ComparisonToolViewModel();

            LogoutCommand = new DelegateCommand(Logout);
            ToggleNavCommand = new DelegateCommand(ToggleNav);
            NavigateMenuCommand = new DelegateCommand<string>(NavigateMenu);
            SelectedMenuItem = "File Explorer";
        }

        private void ToggleNav()
        {
            IsNavCollapsed = !IsNavCollapsed;
        }

        private void Logout()
        {
            _sessionService.CurrentServerName = string.Empty;
            _sessionService.CurrentServerAddress = string.Empty;
            _regionManager.RequestNavigate("MainRegion", "LoginView");
        }

        private void NavigateMenu(string? menuItem)
        {
            if (!string.IsNullOrWhiteSpace(menuItem))
            {
                SelectedMenuItem = menuItem;
            }
        }
    }
}

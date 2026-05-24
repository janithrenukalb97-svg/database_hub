using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System.Windows;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Database_Hub.Views.ConnectedMode;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Database_Hub.Services;
using System;
using System.Linq;
using System.Diagnostics;
using Database_Hub.Views.Common;

namespace Database_Hub.ViewModels
{
    public class ServerInfo
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public override string ToString() => $"{Name}  -->  {Address}";
    }

    public class DatabaseConnectionViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private static readonly object LogLock = new object();
        private static readonly string LogFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "logs",
            $"database-hub-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        private const string ServerListFile = "serverlist.json";

        private ObservableCollection<ServerInfo> _serverList = new ObservableCollection<ServerInfo>();
        public ObservableCollection<ServerInfo> ServerList
        {
            get => _serverList;
            set => SetProperty(ref _serverList, value);
        }

        private ServerInfo? _selectedServer;
        public ServerInfo? SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (SetProperty(ref _selectedServer, value))
                {
                    ServerName = value?.Address ?? string.Empty;
                }
            }
        }

        private string _serverName = string.Empty;
        public string ServerName
        {
            get => _serverName;
            set
            {
                if (SetProperty(ref _serverName, value))
                {
                    ConnectCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand ConnectCommand { get; private set; }
        public DelegateCommand BackCommand { get; private set; }
        public DelegateCommand AddServerCommand { get; private set; }
        public DelegateCommand ResetSelectionCommand { get; private set; }
        public DelegateCommand<ServerInfo> EditServerCommand { get; private set; }
        public DelegateCommand<ServerInfo> DeleteServerCommand { get; private set; }
        public DelegateCommand ViewLatestLogCommand { get; private set; }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }

        private readonly SessionService _sessionService;

        public DatabaseConnectionViewModel(IRegionManager regionManager, SessionService sessionService)
        {
            _regionManager = regionManager;
            _sessionService = sessionService;
            ConnectCommand = new DelegateCommand(Connect);
            BackCommand = new DelegateCommand(NavigateBack);
            AddServerCommand = new DelegateCommand(AddServer);
            ResetSelectionCommand = new DelegateCommand(ResetSelection);
            EditServerCommand = new DelegateCommand<ServerInfo>(EditServer);
            DeleteServerCommand = new DelegateCommand<ServerInfo>(DeleteServer);
            ViewLatestLogCommand = new DelegateCommand(ViewLatestLog);
            LoadServerList();
        }

        private void ResetSelection()
        {
            SelectedServer = null;
            ServerName = string.Empty;
        }

        private static void Log(string message)
        {
            try
            {
                string? logDirectory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, line);
                }
            }
            catch
            {
                // Avoid breaking app flow if logging fails.
            }
        }

        private static void LogException(string prefix, Exception ex)
        {
            Log($"{prefix}: {ex.Message}");
            Log(ex.StackTrace ?? "(no stack trace)");
        }

        private static void ShowErrorDialog(string message, string title = "Error")
        {
            var dialog = new StyledMessageDialog(title, message);
            dialog.ShowDialog();
        }

        private static void ShowToast(string title, string message, ToastNotificationType type = ToastNotificationType.Success)
        {
            var toast = new ToastNotificationWindow(title, message, type);
            toast.Show();
        }

        private void ViewLatestLog()
        {
            try
            {
                var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsDirectory))
                {
                    ShowErrorDialog("No log folder found yet. Try connecting once to create a log file.", "Logs");
                    return;
                }

                var latestLog = new DirectoryInfo(logsDirectory)
                    .GetFiles("*.log")
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();

                if (latestLog == null)
                {
                    ShowErrorDialog("No log file found yet. Try connecting once to create a log file.", "Logs");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = latestLog.FullName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogException("[ERROR] Failed to open latest log", ex);
                ShowErrorDialog($"Could not open the latest log file. {ex.Message}", "Logs");
            }
        }
        private void EditServer(ServerInfo server)
        {
            if (server == null) return;
            var dialog = new EditServerDialog();
            dialog.ServerName = server.Name;
            dialog.ServerAddress = server.Address;
            if (dialog.ShowDialog() == true)
            {
                server.Name = dialog.ServerName;
                server.Address = dialog.ServerAddress;
                SaveServerList();
                // Refresh the list
                var idx = ServerList.IndexOf(server);
                if (idx >= 0)
                {
                    ServerList.RemoveAt(idx);
                    ServerList.Insert(idx, server);
                }

                ShowToast("Server Updated", $"'{server.Name}' has been updated successfully.", ToastNotificationType.Warning);
            }
        }

        private void DeleteServer(ServerInfo server)
        {
            if (server == null) return;
            var dialog = new ConfirmActionDialog("Confirm Delete", $"Delete server '{server.Name}'?");
            if (dialog.ShowDialog() == true)
            {
                ServerList.Remove(server);
                SaveServerList();
                ShowToast("Server Deleted", $"'{server.Name}' has been deleted.", ToastNotificationType.Error);
            }
        }


        private async void Connect()
        {
            try
            {
                string? address = (SelectedServer?.Address ?? ServerName)?.Trim();
                if (address == null)
                {
                    Log("[ERROR] Address is null after trimming.");
                    ShowErrorDialog("Please select or enter a server name.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(address))
                {
                    Log("[ERROR] Address is empty or whitespace.");
                    ShowErrorDialog("Please select or enter a server name.");
                    return;
                }
                // Basic validation: must contain at least a letter or digit
                if (!address.Any(char.IsLetterOrDigit))
                {
                    Log($"[ERROR] Address '{address}' does not contain any letters or digits.");
                    ShowErrorDialog("The server address appears invalid.");
                    return;
                }

                IsConnecting = true;
                // If address contains a comma (port), add tcp: prefix for SQL Server when missing.
                string formattedAddress = address.Contains(",") && !address.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
                    ? $"tcp:{address}"
                    : address;
                if (string.IsNullOrWhiteSpace(formattedAddress))
                {
                    Log("[ERROR] Formatted address is null or whitespace.");
                    ShowErrorDialog("The formatted server address is invalid.");
                    return;
                }
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = formattedAddress,
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    ConnectTimeout = 5,
                    Encrypt = false
                };
                string connectionString = builder.ConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(builder.DataSource))
                {
                    Log("[ERROR] Connection string is invalid: " + connectionString);
                    ShowErrorDialog("The connection string is invalid.");
                    return;
                }
                using (var conn = new SqlConnection(connectionString))
                {
                    try
                    {
                        await conn.OpenAsync();
                    }
                    catch (NullReferenceException ex)
                    {
                        Log("[WARN] OpenAsync hit NullReferenceException, retrying with Open(). " + ex.Message);
                        conn.Open();
                    }
                }
                // Store connection info for future use
                _sessionService.CurrentServerName = address;
                _sessionService.CurrentServerAddress = address;
                var navigationParameters = new NavigationParameters();
                navigationParameters.Add("serverName", address);
                _regionManager.RequestNavigate("MainRegion", "ConnectedHomeView", navigationParameters);
            }
            catch (SqlException ex)
            {
                LogException("[ERROR] Connection failed", ex);
                ShowErrorDialog($"Connection failed: {ex.Message}", "Connection Error");
            }
            catch (Exception ex)
            {
                LogException("[ERROR] Unexpected error", ex);
                ShowErrorDialog($"Unexpected error: {ex.Message}");
            }
            finally
            {
                IsConnecting = false;
            }
        }




        private void AddServer()
        {
            // Prompt user for display name and address
            var inputDialog = new AddServerDialog();
            if (inputDialog.ShowDialog() == true)
            {
                var newServer = new ServerInfo { Name = inputDialog.ServerName, Address = inputDialog.ServerAddress };
                if (!ServerList.Contains(newServer))
                {
                    ServerList.Add(newServer);
                    SaveServerList();
                    ShowToast("Server Added", $"'{newServer.Name}' has been added successfully.");
                }
            }
        }

        private void LoadServerList()
        {
            if (File.Exists(ServerListFile))
            {
                try
                {
                    var json = File.ReadAllText(ServerListFile);
                    var list = JsonSerializer.Deserialize<ObservableCollection<ServerInfo>>(json);
                    if (list != null)
                        ServerList = list;
                }
                catch { }
            }
            else
            {
                // Default servers
                ServerList = new ObservableCollection<ServerInfo>
                {
                    new ServerInfo { Name = "DEV", Address = "SQLNPROD01A\\SQLIDEV,30401" },
                    new ServerInfo { Name = "QA", Address = "SQLQA,30402" },
                    new ServerInfo { Name = "QA02", Address = "SQLQA02,51290" },
                    new ServerInfo { Name = "UAT", Address = "SQLNPROD01C\\SQLIUAT1,30403" },
                    new ServerInfo { Name = "PROD", Address = "SQLSNPT2,40202" }
                };
            }
        }

        private void SaveServerList()
        {
            try
            {
                var json = JsonSerializer.Serialize(ServerList);
                File.WriteAllText(ServerListFile, json);
            }
            catch { }
        }

        private void NavigateBack()
        {
            _regionManager.RequestNavigate("MainRegion", "LoginView");
        }


        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // Called when navigating to this view
            LoadServerList();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // Called when navigating away from this view
        }
    }
}
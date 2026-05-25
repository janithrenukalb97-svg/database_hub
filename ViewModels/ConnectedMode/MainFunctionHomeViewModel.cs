using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class DatabaseObjectInfo : BindableBase
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string SchemaName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string ObjectTypeCode { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
    }

    public class MainFunctionHomeViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly Database_Hub.Services.SessionService _sessionService;
        private string _serverName = string.Empty;
        private string _selectedMenuItem = "Home";
        private string _selectedDatabase = string.Empty;
        private string _schemaFilter = string.Empty;
        private string _keywordFilter = string.Empty;
        private bool _isObjectExplorerBusy;
        private string _objectExplorerStatus = "Select a database to search objects.";
        private CancellationTokenSource? _searchCancellationTokenSource;

        public ObservableCollection<string> DatabaseList { get; } = new ObservableCollection<string>();
        public ObservableCollection<DatabaseObjectInfo> DatabaseObjects { get; } = new ObservableCollection<DatabaseObjectInfo>();

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
                    RaisePropertyChanged(nameof(IsHomeSelected));
                    RaisePropertyChanged(nameof(IsObjectExplorerSelected));
                    RaisePropertyChanged(nameof(IsReleaseExecutorSelected));
                    RaisePropertyChanged(nameof(IsFileExplorerSelected));
                    RaisePropertyChanged(nameof(IsComparisonToolSelected));
                }
            }
        }

        public bool IsHomeSelected => SelectedMenuItem == "Home";
        public bool IsObjectExplorerSelected => SelectedMenuItem == "Object Explorer";
        public bool IsReleaseExecutorSelected => SelectedMenuItem == "Release Executor";
        public bool IsFileExplorerSelected => SelectedMenuItem == "File Explorer";
        public bool IsComparisonToolSelected => SelectedMenuItem == "Comparison Tool";

        public string SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                if (SetProperty(ref _selectedDatabase, value))
                {
                    SearchObjectsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SchemaFilter
        {
            get => _schemaFilter;
            set => SetProperty(ref _schemaFilter, value);
        }

        public string KeywordFilter
        {
            get => _keywordFilter;
            set => SetProperty(ref _keywordFilter, value);
        }

        public bool IsObjectExplorerBusy
        {
            get => _isObjectExplorerBusy;
            set
            {
                if (SetProperty(ref _isObjectExplorerBusy, value))
                {
                    SearchObjectsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string ObjectExplorerStatus
        {
            get => _objectExplorerStatus;
            set => SetProperty(ref _objectExplorerStatus, value);
        }

        public DelegateCommand LogoutCommand { get; private set; }
        public DelegateCommand<string> NavigateMenuCommand { get; private set; }
        public DelegateCommand SearchObjectsCommand { get; private set; }
        public DelegateCommand CancelSearchCommand { get; private set; }
        public DelegateCommand<DatabaseObjectInfo> DownloadObjectScriptCommand { get; private set; }

        public MainFunctionHomeViewModel(Database_Hub.Services.SessionService sessionService, IRegionManager regionManager)
        {
            _sessionService = sessionService;
            _regionManager = regionManager;
            ServerName = _sessionService.CurrentServerName;
            LogoutCommand = new DelegateCommand(Logout);
            NavigateMenuCommand = new DelegateCommand<string>(NavigateMenu);
            SearchObjectsCommand = new DelegateCommand(async () => await SearchObjectsAsync(), CanSearchObjects);
            CancelSearchCommand = new DelegateCommand(CancelSearch);
            DownloadObjectScriptCommand = new DelegateCommand<DatabaseObjectInfo>(async item => await DownloadObjectScriptAsync(item));
            SelectedMenuItem = "Home";
        }

        private void Logout()
        {
            _regionManager.RequestNavigate("MainRegion", "LoginView");
        }

        private void NavigateMenu(string? menuItem)
        {
            if (!string.IsNullOrWhiteSpace(menuItem))
            {
                SelectedMenuItem = menuItem;

                if (menuItem == "Object Explorer")
                {
                    _ = LoadDatabasesAsync();
                }
            }
        }

        private bool CanSearchObjects()
        {
            return !IsObjectExplorerBusy && !string.IsNullOrWhiteSpace(SelectedDatabase);
        }

        private static string BuildConnectionString(string serverAddress, string? database = null)
        {
            var formattedAddress = serverAddress.Contains(",", StringComparison.Ordinal) && !serverAddress.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
                ? $"tcp:{serverAddress}"
                : serverAddress;

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = formattedAddress,
                InitialCatalog = string.IsNullOrWhiteSpace(database) ? "master" : database,
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 10,
                Encrypt = false
            };

            return builder.ConnectionString;
        }

        private async Task LoadDatabasesAsync()
        {
            if (DatabaseList.Count > 0)
            {
                return;
            }

            var serverAddress = _sessionService.CurrentServerAddress;
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                ObjectExplorerStatus = "No active server session found. Please reconnect.";
                return;
            }

            IsObjectExplorerBusy = true;
            ObjectExplorerStatus = "Loading databases...";

            try
            {
                using var connection = new SqlConnection(BuildConnectionString(serverAddress, "master"));
                await connection.OpenAsync();

                using var command = new SqlCommand("SELECT [name] FROM sys.databases WHERE [state] = 0 ORDER BY [name];", connection);
                using var reader = await command.ExecuteReaderAsync();

                DatabaseList.Clear();
                while (await reader.ReadAsync())
                {
                    var databaseName = reader.GetString(0);
                    DatabaseList.Add(databaseName);
                }

                ObjectExplorerStatus = DatabaseList.Count > 0
                    ? "Databases loaded. Select a database and search."
                    : "No online databases found.";
            }
            catch (Exception ex)
            {
                DatabaseList.Clear();
                ObjectExplorerStatus = $"Failed to load databases: {ex.Message}";
            }
            finally
            {
                IsObjectExplorerBusy = false;
            }
        }

        private async Task SearchObjectsAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedDatabase))
            {
                ObjectExplorerStatus = "Database selection is required.";
                return;
            }

            var serverAddress = _sessionService.CurrentServerAddress;
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                ObjectExplorerStatus = "No active server session found. Please reconnect.";
                return;
            }

            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;

            IsObjectExplorerBusy = true;
            ObjectExplorerStatus = "Searching objects...";
            DatabaseObjects.Clear();

            try
            {
                using var connection = new SqlConnection(BuildConnectionString(serverAddress, SelectedDatabase));
                await connection.OpenAsync(cancellationToken);

                var sql = @"
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type AS ObjectTypeCode,
    o.type_desc AS ObjectType
FROM sys.objects o
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0";

                if (!string.IsNullOrWhiteSpace(SchemaFilter))
                {
                    sql += " AND s.name = @schema";
                }

                if (!string.IsNullOrWhiteSpace(KeywordFilter))
                {
                    sql += " AND o.name LIKE @keyword";
                }

                sql += " ORDER BY s.name, o.name;";

                using var command = new SqlCommand(sql, connection);
                if (!string.IsNullOrWhiteSpace(SchemaFilter))
                {
                    command.Parameters.AddWithValue("@schema", SchemaFilter.Trim());
                }

                if (!string.IsNullOrWhiteSpace(KeywordFilter))
                {
                    command.Parameters.AddWithValue("@keyword", $"%{KeywordFilter.Trim()}%");
                }

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DatabaseObjects.Add(new DatabaseObjectInfo
                    {
                        SchemaName = reader.GetString(0),
                        ObjectName = reader.GetString(1),
                        ObjectTypeCode = reader.GetString(2),
                        ObjectType = reader.GetString(3)
                    });
                }

                ObjectExplorerStatus = $"{DatabaseObjects.Count} object(s) found in {SelectedDatabase}.";
            }
            catch (OperationCanceledException)
            {
                ObjectExplorerStatus = "Search canceled.";
            }
            catch (Exception ex)
            {
                ObjectExplorerStatus = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsObjectExplorerBusy = false;
            }
        }

        private void CancelSearch()
        {
            _searchCancellationTokenSource?.Cancel();
            SchemaFilter = string.Empty;
            KeywordFilter = string.Empty;
            DatabaseObjects.Clear();
            ObjectExplorerStatus = "Search canceled and filters cleared.";
        }

        private async Task DownloadObjectScriptAsync(DatabaseObjectInfo? item)
        {
            if (item == null)
            {
                return;
            }

            var serverAddress = _sessionService.CurrentServerAddress;
            if (string.IsNullOrWhiteSpace(serverAddress) || string.IsNullOrWhiteSpace(SelectedDatabase))
            {
                ObjectExplorerStatus = "Missing active connection or selected database.";
                return;
            }

            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Select folder to save object script",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                return;
            }

            try
            {
                using var connection = new SqlConnection(BuildConnectionString(serverAddress, SelectedDatabase));
                await connection.OpenAsync();

                var script = await GetCreateScriptAsync(connection, item);
                if (string.IsNullOrWhiteSpace(script))
                {
                    ObjectExplorerStatus = "No script body found for selected object.";
                    return;
                }

                var safeSchema = SanitizeFileName(item.SchemaName);
                var safeName = SanitizeFileName(item.ObjectName);
                var safeType = SanitizeFileName(item.ObjectType);
                var filePath = Path.Combine(dialog.SelectedPath, $"{safeSchema}.{safeName}.{safeType}.sql");

                await File.WriteAllTextAsync(filePath, script);
                ObjectExplorerStatus = $"Script saved: {filePath}";
            }
            catch (Exception ex)
            {
                ObjectExplorerStatus = $"Download failed: {ex.Message}";
            }
        }

        private static async Task<string> GetCreateScriptAsync(SqlConnection connection, DatabaseObjectInfo item)
        {
            if (item.ObjectTypeCode == "U")
            {
                return await BuildCreateTableScriptAsync(connection, item.SchemaName, item.ObjectName);
            }

            const string sql = @"
SELECT OBJECT_DEFINITION(OBJECT_ID(@fullName));";
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@fullName", $"[{item.SchemaName}].[{item.ObjectName}]");
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }

        private static async Task<string> BuildCreateTableScriptAsync(SqlConnection connection, string schemaName, string tableName)
        {
            const string sql = @"
SELECT
    c.name,
    t.name AS DataType,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    dc.definition
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID(@fullName)
ORDER BY c.column_id;";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@fullName", $"[{schemaName}].[{tableName}]");
            using var reader = await cmd.ExecuteReaderAsync();

            var columnLines = new System.Collections.Generic.List<string>();
            while (await reader.ReadAsync())
            {
                var colName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var maxLength = reader.GetInt16(2);
                var precision = reader.GetByte(3);
                var scale = reader.GetByte(4);
                var isNullable = reader.GetBoolean(5);
                var isIdentity = reader.GetBoolean(6);
                var defaultDef = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);

                var typeSql = FormatSqlType(dataType, maxLength, precision, scale);
                var identitySql = isIdentity ? " IDENTITY(1,1)" : string.Empty;
                var nullableSql = isNullable ? " NULL" : " NOT NULL";
                var defaultSql = string.IsNullOrWhiteSpace(defaultDef) ? string.Empty : $" DEFAULT {defaultDef}";
                columnLines.Add($"    [{colName}] {typeSql}{identitySql}{defaultSql}{nullableSql}");
            }

            if (columnLines.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}]");
            sb.AppendLine("(");
            sb.AppendLine(string.Join("," + Environment.NewLine, columnLines));
            sb.AppendLine(");");
            return sb.ToString();
        }

        private static string FormatSqlType(string dataType, short maxLength, byte precision, byte scale)
        {
            var lower = dataType.ToLowerInvariant();
            if (lower is "nvarchar" or "nchar")
            {
                return maxLength == -1 ? $"{dataType}(MAX)" : $"{dataType}({maxLength / 2})";
            }

            if (lower is "varchar" or "char" or "varbinary" or "binary")
            {
                return maxLength == -1 ? $"{dataType}(MAX)" : $"{dataType}({maxLength})";
            }

            if (lower is "decimal" or "numeric")
            {
                return $"{dataType}({precision},{scale})";
            }

            if (lower is "datetime2" or "datetimeoffset" or "time")
            {
                return $"{dataType}({scale})";
            }

            return dataType;
        }

        private static string SanitizeFileName(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            return new string(chars);
        }
    }
}

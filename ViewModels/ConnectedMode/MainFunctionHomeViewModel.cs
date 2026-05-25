using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
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
        private bool _isAllObjectsSelected;
        private bool _isUpdatingSelection;
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

        public bool IsAllObjectsSelected
        {
            get => _isAllObjectsSelected;
            set
            {
                if (SetProperty(ref _isAllObjectsSelected, value) && !_isUpdatingSelection)
                {
                    ApplySelectAllToRows(value);
                }
            }
        }

        public bool HasSelectedObjects => DatabaseObjects.Any(x => x.IsSelected);

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
        public DelegateCommand ResetSelectionCommand { get; private set; }
        public DelegateCommand DownloadSelectedScriptsCommand { get; private set; }

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
            ResetSelectionCommand = new DelegateCommand(ResetSelectedRows, () => HasSelectedObjects)
                .ObservesProperty(() => IsAllObjectsSelected);
            DownloadSelectedScriptsCommand = new DelegateCommand(async () => await DownloadSelectedScriptsAsync(), () => HasSelectedObjects)
                .ObservesProperty(() => IsAllObjectsSelected);
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
            DetachSelectionHandlers();
            DatabaseObjects.Clear();

            try
            {
                using var connection = new SqlConnection(BuildConnectionString(serverAddress, SelectedDatabase));
                await connection.OpenAsync(cancellationToken);

                var sql = @"
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    RTRIM(o.type) AS ObjectTypeCode,
    o.type_desc AS ObjectType
FROM sys.objects o
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0
    AND o.type IN ('U', 'P', 'V', 'FN', 'IF', 'TF')";

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

                    var row = new DatabaseObjectInfo
                    {
                        SchemaName = reader.GetString(0),
                        ObjectName = reader.GetString(1),
                        ObjectTypeCode = reader.GetString(2),
                        ObjectType = reader.GetString(3)
                    };

                    row.PropertyChanged += OnObjectRowPropertyChanged;
                    DatabaseObjects.Add(row);
                }

                UpdateSelectAllState();
                RefreshSelectionCommands();

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
            DetachSelectionHandlers();
            DatabaseObjects.Clear();
            UpdateSelectAllState();
            RefreshSelectionCommands();
            ObjectExplorerStatus = "Search canceled and filters cleared.";
        }

        private void ApplySelectAllToRows(bool isSelected)
        {
            _isUpdatingSelection = true;
            try
            {
                foreach (var row in DatabaseObjects)
                {
                    row.IsSelected = isSelected;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }

            RefreshSelectionCommands();
        }

        private void OnObjectRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DatabaseObjectInfo.IsSelected) && !_isUpdatingSelection)
            {
                UpdateSelectAllState();
                RefreshSelectionCommands();
            }
        }

        private void UpdateSelectAllState()
        {
            _isUpdatingSelection = true;
            try
            {
                IsAllObjectsSelected = DatabaseObjects.Count > 0 && DatabaseObjects.All(x => x.IsSelected);
            }
            finally
            {
                _isUpdatingSelection = false;
            }

            RaisePropertyChanged(nameof(HasSelectedObjects));
        }

        private void DetachSelectionHandlers()
        {
            foreach (var row in DatabaseObjects)
            {
                row.PropertyChanged -= OnObjectRowPropertyChanged;
            }
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

                var script = await BuildObjectCreateScriptAsync(connection, item);
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

        private async Task DownloadSelectedScriptsAsync()
        {
            var selectedObjects = DatabaseObjects.Where(x => x.IsSelected).ToList();
            if (selectedObjects.Count == 0)
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
                Description = "Select folder to save selected object scripts",
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

                if (selectedObjects.Count == 1)
                {
                    var single = selectedObjects[0];
                    var script = await BuildObjectCreateScriptAsync(connection, single);
                    if (string.IsNullOrWhiteSpace(script))
                    {
                        ObjectExplorerStatus = "No script body found for selected object.";
                        return;
                    }

                    var singleName = BuildScriptFileName(single);
                    var singlePath = Path.Combine(dialog.SelectedPath, singleName);
                    await File.WriteAllTextAsync(singlePath, script);
                    ObjectExplorerStatus = $"Script saved: {singlePath}";
                    return;
                }

                var tempRoot = Path.Combine(Path.GetTempPath(), "DatabaseHub", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                try
                {
                    var generatedFileCount = 0;

                    foreach (var obj in selectedObjects)
                    {
                        var script = await BuildObjectCreateScriptAsync(connection, obj);
                        if (string.IsNullOrWhiteSpace(script))
                        {
                            continue;
                        }

                        var filePath = Path.Combine(tempRoot, BuildScriptFileName(obj));
                        await File.WriteAllTextAsync(filePath, script);
                        generatedFileCount++;
                    }

                    if (generatedFileCount == 0)
                    {
                        ObjectExplorerStatus = "No script body found for selected objects.";
                        return;
                    }

                    var zipFileName = $"{SelectedDatabase}_scripts_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                    var zipPath = Path.Combine(dialog.SelectedPath, zipFileName);

                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }

                    ZipFile.CreateFromDirectory(tempRoot, zipPath, CompressionLevel.Optimal, false);
                    ObjectExplorerStatus = $"Scripts downloaded: {zipPath}";
                }
                finally
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, true);
                    }
                }
            }
            catch (Exception ex)
            {
                ObjectExplorerStatus = $"Download failed: {ex.Message}";
            }
        }

        private static async Task<string> BuildObjectCreateScriptAsync(SqlConnection connection, DatabaseObjectInfo item)
        {
            var normalizedTypeCode = (item.ObjectTypeCode ?? string.Empty).Trim();

            if (string.Equals(normalizedTypeCode, "U", StringComparison.OrdinalIgnoreCase))
            {
                return await BuildCreateTableScriptAsync(connection, item.SchemaName, item.ObjectName);
            }

            const string sql = @"
SELECT m.definition
FROM sys.sql_modules m
INNER JOIN sys.objects o ON o.object_id = m.object_id
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE s.name = @schemaName
  AND o.name = @objectName;";
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@schemaName", item.SchemaName);
            cmd.Parameters.AddWithValue("@objectName", item.ObjectName);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }

        private static async Task<string> BuildCreateTableScriptAsync(SqlConnection connection, string schemaName, string tableName)
        {
            const string tableIdSql = @"
SELECT t.object_id
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @schemaName
  AND t.name = @tableName;";

            using var tableIdCmd = new SqlCommand(tableIdSql, connection);
            tableIdCmd.Parameters.AddWithValue("@schemaName", schemaName);
            tableIdCmd.Parameters.AddWithValue("@tableName", tableName);
            var tableObjectIdObj = await tableIdCmd.ExecuteScalarAsync();
            if (tableObjectIdObj == null || tableObjectIdObj == DBNull.Value)
            {
                return string.Empty;
            }

            var tableObjectId = Convert.ToInt32(tableObjectIdObj);

            var temporalType = 0;
            int? historyTableId = null;

            const string tableMetaSql = @"
SELECT temporal_type, history_table_id
FROM sys.tables
WHERE object_id = @tableObjectId;";

            using (var tableMetaCmd = new SqlCommand(tableMetaSql, connection))
            {
                tableMetaCmd.Parameters.AddWithValue("@tableObjectId", tableObjectId);
                using var tableMetaReader = await tableMetaCmd.ExecuteReaderAsync();
                if (await tableMetaReader.ReadAsync())
                {
                    temporalType = tableMetaReader.GetInt32(0);
                    historyTableId = tableMetaReader.IsDBNull(1) ? null : tableMetaReader.GetInt32(1);
                }
            }

            string historySchemaName = string.Empty;
            string historyTableName = string.Empty;
            if (historyTableId.HasValue)
            {
                const string historySql = @"
SELECT s.name, t.name
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.object_id = @historyTableId;";

                using var historyCmd = new SqlCommand(historySql, connection);
                historyCmd.Parameters.AddWithValue("@historyTableId", historyTableId.Value);
                using var historyReader = await historyCmd.ExecuteReaderAsync();
                if (await historyReader.ReadAsync())
                {
                    historySchemaName = historyReader.GetString(0);
                    historyTableName = historyReader.GetString(1);
                }
            }

            int? periodStartColumnId = null;
            int? periodEndColumnId = null;

            const string periodSql = @"
SELECT start_column_id, end_column_id
FROM sys.periods
WHERE object_id = @tableObjectId;";

            using (var periodCmd = new SqlCommand(periodSql, connection))
            {
                periodCmd.Parameters.AddWithValue("@tableObjectId", tableObjectId);
                using var periodReader = await periodCmd.ExecuteReaderAsync();
                if (await periodReader.ReadAsync())
                {
                    periodStartColumnId = periodReader.GetInt32(0);
                    periodEndColumnId = periodReader.GetInt32(1);
                }
            }

            const string sql = @"
SELECT
    c.column_id,
    c.name,
    t.name AS DataType,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    c.generated_always_type,
    dc.name AS DefaultConstraintName,
    dc.definition
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
WHERE c.object_id = @tableObjectId
ORDER BY c.column_id;";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@tableObjectId", tableObjectId);
            using var reader = await cmd.ExecuteReaderAsync();

            var columnLines = new System.Collections.Generic.List<string>();
            var defaultConstraints = new System.Collections.Generic.List<(string ConstraintName, string ColumnName, string Definition)>();
            string? periodStartColumnName = null;
            string? periodEndColumnName = null;

            while (await reader.ReadAsync())
            {
                var columnId = reader.GetInt32(0);
                var colName = reader.GetString(1);
                var dataType = reader.GetString(2);
                var maxLength = reader.GetInt16(3);
                var precision = reader.GetByte(4);
                var scale = reader.GetByte(5);
                var isNullable = reader.GetBoolean(6);
                var isIdentity = reader.GetBoolean(7);
                var generatedAlwaysType = reader.GetInt32(8);
                var defaultConstraintName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
                var defaultDef = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);

                var typeSql = FormatSqlType(dataType, maxLength, precision, scale);
                var identitySql = isIdentity ? " IDENTITY(1,1)" : string.Empty;
                var generatedSql = generatedAlwaysType switch
                {
                    1 => " GENERATED ALWAYS AS ROW START",
                    2 => " GENERATED ALWAYS AS ROW END",
                    _ => string.Empty
                };
                var nullableSql = isNullable ? " NULL" : " NOT NULL";
                columnLines.Add($"    [{colName}] {typeSql}{identitySql}{generatedSql}{nullableSql}");

                if (!string.IsNullOrWhiteSpace(defaultConstraintName) && !string.IsNullOrWhiteSpace(defaultDef))
                {
                    defaultConstraints.Add((defaultConstraintName, colName, defaultDef));
                }

                if (periodStartColumnId.HasValue && columnId == periodStartColumnId.Value)
                {
                    periodStartColumnName = colName;
                }

                if (periodEndColumnId.HasValue && columnId == periodEndColumnId.Value)
                {
                    periodEndColumnName = colName;
                }
            }

            if (columnLines.Count == 0)
            {
                return string.Empty;
            }

            string? pkConstraintName = null;
            string pkIndexTypeDesc = "CLUSTERED";
            var pkColumns = new System.Collections.Generic.List<string>();

            const string pkHeaderSql = @"
SELECT kc.name, i.type_desc
FROM sys.key_constraints kc
INNER JOIN sys.indexes i
    ON i.object_id = kc.parent_object_id
   AND i.index_id = kc.unique_index_id
WHERE kc.parent_object_id = @tableObjectId
  AND kc.type = 'PK';";

            using (var pkHeaderCmd = new SqlCommand(pkHeaderSql, connection))
            {
                pkHeaderCmd.Parameters.AddWithValue("@tableObjectId", tableObjectId);
                using var pkHeaderReader = await pkHeaderCmd.ExecuteReaderAsync();
                if (await pkHeaderReader.ReadAsync())
                {
                    pkConstraintName = pkHeaderReader.GetString(0);
                    pkIndexTypeDesc = pkHeaderReader.IsDBNull(1) ? "CLUSTERED" : pkHeaderReader.GetString(1);
                }
            }

            if (!string.IsNullOrWhiteSpace(pkConstraintName))
            {
                const string pkColumnsSql = @"
SELECT c.name, ic.is_descending_key
FROM sys.key_constraints kc
INNER JOIN sys.indexes i
    ON i.object_id = kc.parent_object_id
   AND i.index_id = kc.unique_index_id
INNER JOIN sys.index_columns ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE kc.parent_object_id = @tableObjectId
  AND kc.type = 'PK'
ORDER BY ic.key_ordinal;";

                using var pkColumnsCmd = new SqlCommand(pkColumnsSql, connection);
                pkColumnsCmd.Parameters.AddWithValue("@tableObjectId", tableObjectId);
                using var pkColumnsReader = await pkColumnsCmd.ExecuteReaderAsync();
                while (await pkColumnsReader.ReadAsync())
                {
                    var colName = pkColumnsReader.GetString(0);
                    var isDescending = pkColumnsReader.GetBoolean(1);
                    pkColumns.Add($"[{colName}] {(isDescending ? "DESC" : "ASC")}");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}]");
            sb.AppendLine("(");

            var tableBodyLines = new System.Collections.Generic.List<string>(columnLines);

            if (!string.IsNullOrWhiteSpace(pkConstraintName) && pkColumns.Count > 0)
            {
                var pkType = pkIndexTypeDesc.Contains("NONCLUSTERED", StringComparison.OrdinalIgnoreCase)
                    ? "NONCLUSTERED"
                    : "CLUSTERED";

                tableBodyLines.Add($"    CONSTRAINT [{pkConstraintName}] PRIMARY KEY {pkType}");
                tableBodyLines.Add("    (");
                tableBodyLines.Add($"        {string.Join("," + Environment.NewLine + "        ", pkColumns)}");
                tableBodyLines.Add("    )");
            }

            if (!string.IsNullOrWhiteSpace(periodStartColumnName) && !string.IsNullOrWhiteSpace(periodEndColumnName))
            {
                tableBodyLines.Add($"    PERIOD FOR SYSTEM_TIME ([{periodStartColumnName}], [{periodEndColumnName}])");
            }

            sb.AppendLine(string.Join("," + Environment.NewLine, tableBodyLines));
            sb.Append(") ON [PRIMARY]");

            if (temporalType == 2 && !string.IsNullOrWhiteSpace(historySchemaName) && !string.IsNullOrWhiteSpace(historyTableName))
            {
                sb.AppendLine();
                sb.AppendLine("WITH");
                sb.AppendLine("(");
                sb.AppendLine($"SYSTEM_VERSIONING = ON (HISTORY_TABLE = [{historySchemaName}].[{historyTableName}])");
                sb.AppendLine(")");
            }

            sb.AppendLine();
            sb.AppendLine("GO");

            foreach (var constraint in defaultConstraints)
            {
                sb.AppendLine();
                sb.AppendLine($"ALTER TABLE [{schemaName}].[{tableName}] ADD CONSTRAINT [{constraint.ConstraintName}] DEFAULT {constraint.Definition} FOR [{constraint.ColumnName}]");
                sb.AppendLine("GO");
            }

            return sb.ToString();
        }

        private static string FormatSqlType(string dataType, short maxLength, byte precision, byte scale)
        {
            var lower = dataType.ToLowerInvariant();
            if (lower is "nvarchar" or "nchar")
            {
                return maxLength == -1 ? $"[{dataType}](MAX)" : $"[{dataType}]({maxLength / 2})";
            }

            if (lower is "varchar" or "char" or "varbinary" or "binary")
            {
                return maxLength == -1 ? $"[{dataType}](MAX)" : $"[{dataType}]({maxLength})";
            }

            if (lower is "decimal" or "numeric")
            {
                return $"[{dataType}]({precision},{scale})";
            }

            if (lower is "datetime2" or "datetimeoffset" or "time")
            {
                return $"[{dataType}]({scale})";
            }

            return $"[{dataType}]";
        }

        private static string SanitizeFileName(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            return new string(chars);
        }

        private static string BuildScriptFileName(DatabaseObjectInfo item)
        {
            var safeSchema = SanitizeFileName(item.SchemaName);
            var safeName = SanitizeFileName(item.ObjectName);
            var safeType = SanitizeFileName(item.ObjectType);
            return $"{safeSchema}.{safeName}.{safeType}.sql";
        }

        private void ResetSelectedRows()
        {
            _isUpdatingSelection = true;
            try
            {
                foreach (var row in DatabaseObjects)
                {
                    row.IsSelected = false;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }

            UpdateSelectAllState();
            RefreshSelectionCommands();
        }

        private void RefreshSelectionCommands()
        {
            RaisePropertyChanged(nameof(HasSelectedObjects));
            ResetSelectionCommand.RaiseCanExecuteChanged();
            DownloadSelectedScriptsCommand.RaiseCanExecuteChanged();
        }
    }
}

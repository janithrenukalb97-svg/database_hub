using Prism.Commands;
using Prism.Mvvm;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class ReleaseTreeNode : BindableBase
    {
        private bool _isExpanded;

        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public ObservableCollection<ReleaseTreeNode> Children { get; } = new ObservableCollection<ReleaseTreeNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
    }

    public class ReleaseExecutorViewModel : BindableBase
    {
        private readonly Database_Hub.Services.ActionLoggerService _actionLogger;
        private readonly Database_Hub.Services.SessionService _sessionService;
        private string _releaseExecutorStatus = "Select a folder to load SQL files.";
        private string _releaseSelectedFolder = string.Empty;
        private string _manualFolderPath = string.Empty;
        private bool _isExecuting;
        private bool _isExecutionConsoleVisible;
        private int _executionPanelHeight;
        private bool _isPreviewVisible;
        private string _previewScriptTitle = string.Empty;
        private string _previewScriptContent = string.Empty;
        private GridLength _treePaneWidth = new GridLength(1, GridUnitType.Star);
        private GridLength _previewPaneWidth = new GridLength(0, GridUnitType.Pixel);

        public ObservableCollection<ReleaseTreeNode> ReleaseRootNodes { get; } = new ObservableCollection<ReleaseTreeNode>();
        public ObservableCollection<string> ExecutionConsoleLines { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ExecutionLogEntries { get; } = new ObservableCollection<string>();

        public string ReleaseExecutorStatus
        {
            get => _releaseExecutorStatus;
            set => SetProperty(ref _releaseExecutorStatus, value);
        }

        public string ReleaseSelectedFolder
        {
            get => _releaseSelectedFolder;
            set => SetProperty(ref _releaseSelectedFolder, value);
        }

        public string ManualFolderPath
        {
            get => _manualFolderPath;
            set
            {
                if (SetProperty(ref _manualFolderPath, value))
                {
                    LoadScriptsFromPathCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (SetProperty(ref _isExecuting, value))
                {
                    ExecuteScriptsCommand.RaiseCanExecuteChanged();
                    DownloadExecutionLogCommand.RaiseCanExecuteChanged();
                    LoadScriptsFromPathCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsExecutionConsoleVisible
        {
            get => _isExecutionConsoleVisible;
            set => SetProperty(ref _isExecutionConsoleVisible, value);
        }

        public int ExecutionPanelHeight
        {
            get => _executionPanelHeight;
            set => SetProperty(ref _executionPanelHeight, value);
        }

        public bool HasExecutionLog => ExecutionLogEntries.Count > 0;

        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            set => SetProperty(ref _isPreviewVisible, value);
        }

        public string PreviewScriptTitle
        {
            get => _previewScriptTitle;
            set => SetProperty(ref _previewScriptTitle, value);
        }

        public string PreviewScriptContent
        {
            get => _previewScriptContent;
            set => SetProperty(ref _previewScriptContent, value);
        }

        public GridLength TreePaneWidth
        {
            get => _treePaneWidth;
            set => SetProperty(ref _treePaneWidth, value);
        }

        public GridLength PreviewPaneWidth
        {
            get => _previewPaneWidth;
            set => SetProperty(ref _previewPaneWidth, value);
        }

        public DelegateCommand OpenReleaseFolderCommand { get; }
        public DelegateCommand ResetReleaseViewCommand { get; }
        public DelegateCommand LoadScriptsFromPathCommand { get; }
        public DelegateCommand ExecuteScriptsCommand { get; }
        public DelegateCommand DownloadExecutionLogCommand { get; }
        public DelegateCommand<ReleaseTreeNode> PreviewScriptCommand { get; }
        public DelegateCommand ClosePreviewCommand { get; }

        public ReleaseExecutorViewModel(
            Database_Hub.Services.ActionLoggerService actionLogger,
            Database_Hub.Services.SessionService sessionService)
        {
            _actionLogger = actionLogger;
            _sessionService = sessionService;
            OpenReleaseFolderCommand = new DelegateCommand(OpenReleaseFolder);
            ResetReleaseViewCommand = new DelegateCommand(ResetReleaseView);
            LoadScriptsFromPathCommand = new DelegateCommand(LoadScriptsFromPath, CanLoadScriptsFromPath);
            ExecuteScriptsCommand = new DelegateCommand(async () => await ExecuteScriptsAsync(), CanExecuteScripts);
            DownloadExecutionLogCommand = new DelegateCommand(DownloadExecutionLog, CanDownloadExecutionLog);
            PreviewScriptCommand = new DelegateCommand<ReleaseTreeNode>(async node => await PreviewScriptAsync(node));
            ClosePreviewCommand = new DelegateCommand(ClosePreview);
            ExecutionPanelHeight = 0;
        }

        private void ResetReleaseView()
        {
            ReleaseRootNodes.Clear();
            ReleaseSelectedFolder = string.Empty;
            ManualFolderPath = string.Empty;
            ReleaseExecutorStatus = "Select a folder to load SQL files.";
            ExecutionConsoleLines.Clear();
            ExecutionLogEntries.Clear();
            IsExecutionConsoleVisible = false;
            ExecutionPanelHeight = 0;
            ClosePreview();
            RaisePropertyChanged(nameof(HasExecutionLog));
            ExecuteScriptsCommand.RaiseCanExecuteChanged();
            DownloadExecutionLogCommand.RaiseCanExecuteChanged();
            LoadScriptsFromPathCommand.RaiseCanExecuteChanged();
            _actionLogger.LogAction("RELEASE_RESET", "SUCCESS", "Release Executor view reset by user.");
        }

        private void OpenReleaseFolder()
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Select release folder containing SQL files",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                _actionLogger.LogAction("RELEASE_FOLDER", "CANCELED", "Folder selection canceled.");
                return;
            }

            try
            {
                var selectedFolder = dialog.SelectedPath;
                ManualFolderPath = selectedFolder;
                LoadScriptsFromFolder(selectedFolder, "RELEASE_FOLDER");
            }
            catch (Exception ex)
            {
                ReleaseRootNodes.Clear();
                ReleaseExecutorStatus = $"Failed to load SQL files: {ex.Message}";
                _actionLogger.LogException("RELEASE_FOLDER", ex, "Failed to scan folder recursively.");
                ExecuteScriptsCommand.RaiseCanExecuteChanged();
            }
        }

        private bool CanLoadScriptsFromPath()
        {
            return !IsExecuting && !string.IsNullOrWhiteSpace(ManualFolderPath);
        }

        private void LoadScriptsFromPath()
        {
            var path = (ManualFolderPath ?? string.Empty).Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(path))
            {
                ReleaseExecutorStatus = "Please enter a folder path.";
                return;
            }

            if (!Directory.Exists(path))
            {
                ReleaseExecutorStatus = $"Folder not found: {path}";
                _actionLogger.LogAction("RELEASE_LOAD_PATH", "FAILED", $"FolderNotFound={path}");
                return;
            }

            try
            {
                LoadScriptsFromFolder(path, "RELEASE_LOAD_PATH");
            }
            catch (Exception ex)
            {
                ReleaseRootNodes.Clear();
                ReleaseExecutorStatus = $"Failed to load SQL files: {ex.Message}";
                _actionLogger.LogException("RELEASE_LOAD_PATH", ex, "Failed to load scripts from input path.");
                ExecuteScriptsCommand.RaiseCanExecuteChanged();
            }
        }

        private void LoadScriptsFromFolder(string selectedFolder, string actionName)
        {
            ReleaseSelectedFolder = selectedFolder;
            ManualFolderPath = selectedFolder;
            ReleaseRootNodes.Clear();
            ClosePreview();

            var rootDirectory = new DirectoryInfo(selectedFolder);
            var sqlFileCount = BuildTree(rootDirectory, ReleaseRootNodes);

            if (sqlFileCount == 0)
            {
                ReleaseExecutorStatus = "No .sql files found in the selected folder.";
            }
            else
            {
                ReleaseExecutorStatus = $"Loaded {sqlFileCount} .sql file(s) from {selectedFolder}";
            }

            _actionLogger.LogAction(actionName, "SUCCESS", $"Folder={selectedFolder}; Files={sqlFileCount}");
            ExecuteScriptsCommand.RaiseCanExecuteChanged();
        }

        private bool CanExecuteScripts()
        {
            return !IsExecuting
                && !string.IsNullOrWhiteSpace(ReleaseSelectedFolder)
                && FlattenFileNodes(ReleaseRootNodes).Any();
        }

        private bool CanDownloadExecutionLog()
        {
            return !IsExecuting && HasExecutionLog;
        }

        private async Task PreviewScriptAsync(ReleaseTreeNode? node)
        {
            if (node == null || node.IsFolder)
            {
                return;
            }

            if (!File.Exists(node.FullPath))
            {
                ReleaseExecutorStatus = $"File not found: {node.FullPath}";
                return;
            }

            try
            {
                var content = await File.ReadAllTextAsync(node.FullPath);
                PreviewScriptTitle = node.Name;
                PreviewScriptContent = content;
                IsPreviewVisible = true;
                TreePaneWidth = new GridLength(1, GridUnitType.Star);
                PreviewPaneWidth = new GridLength(1, GridUnitType.Star);
            }
            catch (Exception ex)
            {
                ReleaseExecutorStatus = $"Failed to preview script: {ex.Message}";
                _actionLogger.LogException("RELEASE_PREVIEW", ex, $"File={node.FullPath}");
            }
        }

        private void ClosePreview()
        {
            PreviewScriptTitle = string.Empty;
            PreviewScriptContent = string.Empty;
            IsPreviewVisible = false;
            TreePaneWidth = new GridLength(1, GridUnitType.Star);
            PreviewPaneWidth = new GridLength(0, GridUnitType.Pixel);
        }

        private async Task ExecuteScriptsAsync()
        {
            var serverAddress = _sessionService.CurrentServerAddress;
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                ReleaseExecutorStatus = "No active server session found. Please reconnect.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ReleaseSelectedFolder) || !Directory.Exists(ReleaseSelectedFolder))
            {
                ReleaseExecutorStatus = "Please select a valid folder before execution.";
                return;
            }

            var filesToExecute = FlattenFileNodes(ReleaseRootNodes).ToList();
            if (filesToExecute.Count == 0)
            {
                ReleaseExecutorStatus = "No SQL files available for execution.";
                return;
            }

            IsExecuting = true;
            IsExecutionConsoleVisible = true;
            ExecutionPanelHeight = 220;
            ExecutionConsoleLines.Clear();
            ExecutionLogEntries.Clear();
            RaisePropertyChanged(nameof(HasExecutionLog));

            var startedAt = DateTime.Now;
            var doneRootFolder = Path.Combine(ReleaseSelectedFolder, "Excution Done");
            Directory.CreateDirectory(doneRootFolder);

            var successCount = 0;
            var failedCount = 0;

            AppendConsole($"[{startedAt:yyyy-MM-dd HH:mm:ss}] Execution started. Files={filesToExecute.Count}");
            AppendConsole($"[{startedAt:yyyy-MM-dd HH:mm:ss}] Done folder prepared: {doneRootFolder}");
            _actionLogger.LogAction("RELEASE_EXECUTE", "STARTED", $"Folder={ReleaseSelectedFolder}; Files={filesToExecute.Count}");

            foreach (var fileNode in filesToExecute)
            {
                var filePath = fileNode.FullPath;
                var relativePath = Path.GetRelativePath(ReleaseSelectedFolder, filePath).Replace('\\', '/');
                var timestamp = DateTime.Now;

                if (!File.Exists(filePath))
                {
                    failedCount++;
                    var missingMessage = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {relativePath} -> FAILED (File not found)";
                    AppendConsole(missingMessage);
                    ExecutionLogEntries.Add($"{timestamp:yyyy-MM-dd HH:mm:ss} --> {relativePath} --> EXECUTION_FAILED --> File not found");
                    continue;
                }

                try
                {
                    AppendConsole($"[{timestamp:yyyy-MM-dd HH:mm:ss}] Executing {relativePath}");
                    await ExecuteSqlFileAsync(filePath, serverAddress);
                    MoveFileToDone(filePath, ReleaseSelectedFolder, doneRootFolder);

                    successCount++;
                    var successTime = DateTime.Now;
                    AppendConsole($"[{successTime:yyyy-MM-dd HH:mm:ss}] {relativePath} -> SUCCESS");
                    ExecutionLogEntries.Add($"{successTime:yyyy-MM-dd HH:mm:ss} --> {relativePath} --> EXECUTION_SUCCESS");
                }
                catch (Exception ex)
                {
                    failedCount++;
                    var failedTime = DateTime.Now;
                    AppendConsole($"[{failedTime:yyyy-MM-dd HH:mm:ss}] {relativePath} -> FAILED ({ex.Message})");
                    ExecutionLogEntries.Add($"{failedTime:yyyy-MM-dd HH:mm:ss} --> {relativePath} --> EXECUTION_FAILED --> {ex.Message}");
                    _actionLogger.LogException("RELEASE_EXECUTE_FILE", ex, $"File={relativePath}");
                }
            }

            var endedAt = DateTime.Now;
            ReleaseExecutorStatus = $"Execution completed. Success={successCount}, Failed={failedCount}";
            AppendConsole($"[{endedAt:yyyy-MM-dd HH:mm:ss}] Execution finished. Success={successCount}, Failed={failedCount}");

            _actionLogger.LogAction(
                "RELEASE_EXECUTE",
                failedCount == 0 ? "SUCCESS" : "PARTIAL",
                $"Folder={ReleaseSelectedFolder}; Success={successCount}; Failed={failedCount}");

            try
            {
                var rootDirectory = new DirectoryInfo(ReleaseSelectedFolder);
                ReleaseRootNodes.Clear();
                BuildTree(rootDirectory, ReleaseRootNodes);
            }
            catch (Exception ex)
            {
                _actionLogger.LogException("RELEASE_REFRESH", ex, "Failed to refresh tree after execution.");
            }

            RaisePropertyChanged(nameof(HasExecutionLog));
            IsExecuting = false;
            ExecuteScriptsCommand.RaiseCanExecuteChanged();
            DownloadExecutionLogCommand.RaiseCanExecuteChanged();
        }

        private void DownloadExecutionLog()
        {
            if (ExecutionConsoleLines.Count == 0)
            {
                return;
            }

            using var dialog = new Forms.SaveFileDialog
            {
                Title = "Save execution log",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"release-execution-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                _actionLogger.LogAction("RELEASE_EXEC_LOG", "CANCELED", "Execution log download canceled.");
                return;
            }

            try
            {
                File.WriteAllLines(dialog.FileName, ExecutionConsoleLines, Encoding.UTF8);
                _actionLogger.LogAction("RELEASE_EXEC_LOG", "SUCCESS", $"Saved={dialog.FileName}; Lines={ExecutionConsoleLines.Count}");
                ReleaseExecutorStatus = $"Execution log saved: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                _actionLogger.LogException("RELEASE_EXEC_LOG", ex, "Failed to save execution log.");
                ReleaseExecutorStatus = $"Failed to save execution log: {ex.Message}";
                MessageBox.Show($"Failed to save execution log.\n\n{ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string BuildConnectionString(string serverAddress)
        {
            var formattedAddress = serverAddress.Contains(",", StringComparison.Ordinal) && !serverAddress.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
                ? $"tcp:{serverAddress}"
                : serverAddress;

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = formattedAddress,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 15,
                Encrypt = false
            };

            return builder.ConnectionString;
        }

        private static IEnumerable<ReleaseTreeNode> FlattenFileNodes(IEnumerable<ReleaseTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsFolder)
                {
                    foreach (var child in FlattenFileNodes(node.Children))
                    {
                        yield return child;
                    }
                }
                else
                {
                    yield return node;
                }
            }
        }

        private static async Task ExecuteSqlFileAsync(string filePath, string serverAddress)
        {
            var script = await File.ReadAllTextAsync(filePath);
            var batches = SplitBatches(script);

            using var connection = new SqlConnection(BuildConnectionString(serverAddress));
            await connection.OpenAsync();

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                using var command = new SqlCommand(batch, connection)
                {
                    CommandTimeout = 300
                };

                await command.ExecuteNonQueryAsync();
            }
        }

        private static List<string> SplitBatches(string script)
        {
            var batches = new List<string>();
            var current = new StringBuilder();

            using var reader = new StringReader(script);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    var batch = current.ToString().Trim();
                    if (batch.Length > 0)
                    {
                        batches.Add(batch);
                    }

                    current.Clear();
                    continue;
                }

                current.AppendLine(line);
            }

            var finalBatch = current.ToString().Trim();
            if (finalBatch.Length > 0)
            {
                batches.Add(finalBatch);
            }

            return batches;
        }

        private static void MoveFileToDone(string sourceFilePath, string rootFolder, string doneRootFolder)
        {
            var relativePath = Path.GetRelativePath(rootFolder, sourceFilePath);
            var targetPath = Path.Combine(doneRootFolder, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(sourceFilePath, targetPath);
        }

        private void AppendConsole(string message)
        {
            ExecutionConsoleLines.Add(message);
        }

        private static int BuildTree(DirectoryInfo directory, ObservableCollection<ReleaseTreeNode> target)
        {
            var sqlCount = 0;

            var directories = directory
                .GetDirectories()
                .Where(d =>
                    !d.Name.Equals("Excution Done", StringComparison.OrdinalIgnoreCase)
                    && !d.Name.Equals("Done", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var subDirectory in directories)
            {
                var folderNode = new ReleaseTreeNode
                {
                    Name = subDirectory.Name,
                    FullPath = subDirectory.FullName,
                    IsFolder = true,
                    IsExpanded = true
                };

                var childSqlCount = BuildTree(subDirectory, folderNode.Children);
                if (childSqlCount > 0)
                {
                    target.Add(folderNode);
                    sqlCount += childSqlCount;
                }
            }

            var sqlFiles = directory
                .GetFiles("*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in sqlFiles)
            {
                target.Add(new ReleaseTreeNode
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsFolder = false
                });
            }

            sqlCount += sqlFiles.Count;
            return sqlCount;
        }
    }
}

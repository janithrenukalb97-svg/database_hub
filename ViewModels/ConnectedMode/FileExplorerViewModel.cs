using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class FilePreviewTab : BindableBase
    {
        private string _content = string.Empty;
        private string _error = string.Empty;

        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public string Error
        {
            get => _error;
            set
            {
                if (SetProperty(ref _error, value))
                {
                    RaisePropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(Error);
    }

    public class FileExplorerNode : BindableBase
    {
        private bool _isExpanded;

        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public ObservableCollection<FileExplorerNode> Children { get; } = new ObservableCollection<FileExplorerNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
    }

    public class FileExplorerViewModel : BindableBase
    {
        private const long PreviewMaxBytes = 2 * 1024 * 1024;

        private readonly Database_Hub.Services.ActionLoggerService _actionLogger;
        private string _selectedFolderPath = string.Empty;
        private string _manualFolderPath = string.Empty;
        private FilePreviewTab? _selectedPreviewTab;

        private static readonly HashSet<string> ReadableCodeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".xaml", ".xml", ".json", ".sql", ".txt", ".md", ".js", ".ts", ".jsx", ".tsx",
            ".html", ".css", ".scss", ".less", ".yml", ".yaml", ".ps1", ".bat", ".cmd", ".config",
            ".csproj", ".sln", ".java", ".py", ".cpp", ".c", ".h", ".hpp", ".go", ".rs", ".vb",
            ".ini", ".toml", ".sh", ".sqlproj", ".props", ".targets", ".editorconfig", ".gitignore"
        };

        public string Title { get; } = "File Explorer";

        public ObservableCollection<FileExplorerNode> RootNodes { get; } = new ObservableCollection<FileExplorerNode>();

        public string SelectedFolderPath
        {
            get => _selectedFolderPath;
            set
            {
                if (SetProperty(ref _selectedFolderPath, value))
                {
                    RaisePropertyChanged(nameof(HasLoadedFolder));
                }
            }
        }

        public string ManualFolderPath
        {
            get => _manualFolderPath;
            set
            {
                if (SetProperty(ref _manualFolderPath, value))
                {
                    LoadFromPathCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<FilePreviewTab> PreviewTabs { get; } = new ObservableCollection<FilePreviewTab>();

        public FilePreviewTab? SelectedPreviewTab
        {
            get => _selectedPreviewTab;
            set => SetProperty(ref _selectedPreviewTab, value);
        }

        public bool HasLoadedFolder => !string.IsNullOrWhiteSpace(SelectedFolderPath);

        public DelegateCommand OpenFolderCommand { get; }
        public DelegateCommand LoadFromPathCommand { get; }
        public DelegateCommand<FileExplorerNode> PreviewFileCommand { get; }
        public DelegateCommand<FilePreviewTab> ClosePreviewTabCommand { get; }
        public DelegateCommand ResetExplorerCommand { get; }

        public FileExplorerViewModel(Database_Hub.Services.ActionLoggerService actionLogger)
        {
            _actionLogger = actionLogger;
            OpenFolderCommand = new DelegateCommand(OpenFolder);
            LoadFromPathCommand = new DelegateCommand(LoadFromPath, CanLoadFromPath);
            PreviewFileCommand = new DelegateCommand<FileExplorerNode>(async node => await PreviewFileAsync(node));
            ClosePreviewTabCommand = new DelegateCommand<FilePreviewTab>(ClosePreviewTab);
            ResetExplorerCommand = new DelegateCommand(ResetExplorer);
        }

        private void OpenFolder()
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "Select folder to explore files",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                _actionLogger.LogAction("FILE_EXPLORER_FOLDER", "CANCELED", "Folder selection canceled.");
                return;
            }

            try
            {
                LoadFolder(dialog.SelectedPath, "FILE_EXPLORER_FOLDER");
            }
            catch (Exception ex)
            {
                _actionLogger.LogException("FILE_EXPLORER_FOLDER", ex, "Failed to load selected folder.");
            }
        }

        private bool CanLoadFromPath()
        {
            return !string.IsNullOrWhiteSpace(ManualFolderPath);
        }

        private void LoadFromPath()
        {
            var path = (ManualFolderPath ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!Directory.Exists(path))
            {
                _actionLogger.LogAction("FILE_EXPLORER_LOAD_PATH", "FAILED", $"FolderNotFound={path}");
                return;
            }

            try
            {
                LoadFolder(path, "FILE_EXPLORER_LOAD_PATH");
            }
            catch (Exception ex)
            {
                _actionLogger.LogException("FILE_EXPLORER_LOAD_PATH", ex, "Failed to load path from textbox.");
            }
        }

        private void LoadFolder(string folderPath, string actionName)
        {
            SelectedFolderPath = folderPath;
            ManualFolderPath = folderPath;
            RootNodes.Clear();
            PreviewTabs.Clear();
            SelectedPreviewTab = null;
            BuildTree(new DirectoryInfo(folderPath), RootNodes);
            _actionLogger.LogAction(actionName, "SUCCESS", $"Folder={folderPath}");
        }

        private void ResetExplorer()
        {
            RootNodes.Clear();
            PreviewTabs.Clear();
            SelectedPreviewTab = null;
            SelectedFolderPath = string.Empty;
            ManualFolderPath = string.Empty;
            _actionLogger.LogAction("FILE_EXPLORER_RESET", "SUCCESS", "File explorer reset by user.");
        }

        private async Task PreviewFileAsync(FileExplorerNode? node)
        {
            if (node == null || node.IsFolder)
            {
                return;
            }

            var existingTab = PreviewTabs.FirstOrDefault(t => string.Equals(t.FullPath, node.FullPath, StringComparison.OrdinalIgnoreCase));
            if (existingTab != null)
            {
                SelectedPreviewTab = existingTab;
                return;
            }

            var previewTab = new FilePreviewTab
            {
                FileName = node.Name,
                FullPath = node.FullPath
            };
            PreviewTabs.Add(previewTab);
            SelectedPreviewTab = previewTab;

            if (!File.Exists(node.FullPath))
            {
                previewTab.Error = "Cannot preview: file not found.";
                return;
            }

            var extension = Path.GetExtension(node.FullPath);
            if (!ReadableCodeExtensions.Contains(extension))
            {
                previewTab.Error = "Cannot preview this file type.";
                return;
            }

            try
            {
                var fileInfo = new FileInfo(node.FullPath);
                if (fileInfo.Length > PreviewMaxBytes)
                {
                    previewTab.Error = "Cannot preview: file is too large to display.";
                    return;
                }

                previewTab.Content = await File.ReadAllTextAsync(node.FullPath);
                if (string.IsNullOrWhiteSpace(previewTab.Content))
                {
                    previewTab.Content = "(File is empty)";
                }
            }
            catch (Exception ex)
            {
                previewTab.Error = $"Cannot preview this file. {ex.Message}";
                _actionLogger.LogException("FILE_EXPLORER_PREVIEW", ex, $"File={node.FullPath}");
            }
        }

        private void ClosePreviewTab(FilePreviewTab? tab)
        {
            if (tab == null)
            {
                return;
            }

            var removedIndex = PreviewTabs.IndexOf(tab);
            if (removedIndex < 0)
            {
                return;
            }

            var wasSelected = ReferenceEquals(SelectedPreviewTab, tab);
            PreviewTabs.RemoveAt(removedIndex);

            if (!wasSelected)
            {
                return;
            }

            if (PreviewTabs.Count == 0)
            {
                SelectedPreviewTab = null;
                return;
            }

            var nextIndex = Math.Min(removedIndex, PreviewTabs.Count - 1);
            SelectedPreviewTab = PreviewTabs[nextIndex];
        }

        private static void BuildTree(DirectoryInfo directory, ObservableCollection<FileExplorerNode> target)
        {
            var subDirectories = directory
                .GetDirectories()
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var subDirectory in subDirectories)
            {
                var folderNode = new FileExplorerNode
                {
                    Name = subDirectory.Name,
                    FullPath = subDirectory.FullName,
                    IsFolder = true,
                    IsExpanded = false
                };

                BuildTree(subDirectory, folderNode.Children);
                target.Add(folderNode);
            }

            var files = directory
                .GetFiles("*", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                target.Add(new FileExplorerNode
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsFolder = false
                });
            }
        }
    }
}

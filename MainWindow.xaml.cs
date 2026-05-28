using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Database_Hub.Services;
using System.ComponentModel;

namespace Database_Hub
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isWindowedMaximized;
        private Rect _restoreBounds;
        private SessionService? _sessionService;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => UpdateWindowChromeForState();
        }

        public void InitializeSession(SessionService sessionService)
        {
            if (_sessionService != null)
            {
                _sessionService.PropertyChanged -= SessionService_PropertyChanged;
            }

            _sessionService = sessionService;
            _sessionService.PropertyChanged += SessionService_PropertyChanged;
            UpdateConnectionStatusText();
        }

        private void SessionService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionService.CurrentServerName) ||
                e.PropertyName == nameof(SessionService.CurrentServerAddress))
            {
                Dispatcher.Invoke(UpdateConnectionStatusText);
            }
        }

        private void UpdateConnectionStatusText()
        {
            var serverName = _sessionService?.CurrentServerName;
            if (string.IsNullOrWhiteSpace(serverName))
            {
                ConnectionStatusText.Text = string.Empty;
                ConnectionStatusText.Visibility = Visibility.Collapsed;
                return;
            }

            ConnectionStatusText.Text = $"| Connected Mode - {serverName}";
            ConnectionStatusText.Visibility = Visibility.Visible;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximizeRestore()
        {
            if (_isWindowedMaximized)
            {
                RestoreFromWorkArea();
            }
            else
            {
                MaximizeToWorkArea();
            }
        }

        private void MaximizeToWorkArea()
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);

            var workArea = SystemParameters.WorkArea;
            WindowState = WindowState.Normal;
            Left = workArea.Left;
            Top = workArea.Top;
            Width = workArea.Width;
            Height = workArea.Height;

            _isWindowedMaximized = true;
            UpdateWindowChromeForState();
        }

        private void RestoreFromWorkArea()
        {
            WindowState = WindowState.Normal;

            if (_restoreBounds.Width > 0 && _restoreBounds.Height > 0)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
            }

            _isWindowedMaximized = false;
            UpdateWindowChromeForState();
        }

        private void UpdateWindowChromeForState()
        {
            WindowRootBorder.BorderThickness = _isWindowedMaximized ? new Thickness(0) : new Thickness(1);

            if (MaximizeRestoreButton != null)
            {
                MaximizeRestoreButton.Content = _isWindowedMaximized ? "❐" : "□";
            }
        }
    }
}

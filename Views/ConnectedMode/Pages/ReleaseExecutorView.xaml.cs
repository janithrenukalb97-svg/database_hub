using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Database_Hub.ViewModels.ConnectedMode;

namespace Database_Hub.Views.ConnectedMode.Pages
{
    public partial class ReleaseExecutorView : UserControl
    {
        public ReleaseExecutorView()
        {
            InitializeComponent();
        }

        private void ReleaseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not ReleaseExecutorViewModel vm)
            {
                return;
            }

            if (e.NewValue is not ReleaseTreeNode node || node.IsFolder || !node.IsSqlFile)
            {
                return;
            }

            if (vm.PreviewScriptCommand.CanExecute(node))
            {
                vm.PreviewScriptCommand.Execute(node);
            }
        }

        private void ScriptButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DependencyObject source)
            {
                return;
            }

            var treeItem = FindAncestor<TreeViewItem>(source);
            if (treeItem != null)
            {
                treeItem.IsSelected = true;
                treeItem.Focus();
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(current);

            while (parent != null)
            {
                if (parent is T typed)
                {
                    return typed;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }
    }
}

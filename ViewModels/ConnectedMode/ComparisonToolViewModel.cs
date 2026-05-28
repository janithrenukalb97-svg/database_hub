using Prism.Mvvm;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class ComparisonToolViewModel : BindableBase
    {
        public string Title { get; } = "Comparison Tool";
        public string Description { get; } = "Compare scripts, objects, and file changes from this workspace.";
    }
}

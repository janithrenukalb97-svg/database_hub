using Prism.Mvvm;

namespace Database_Hub.ViewModels.ConnectedMode
{
    public class HomePageViewModel : BindableBase
    {
        public string Title { get; } = "Main Function Home";
        public string Description { get; } = "This is the main function home page for connected mode.";
    }
}

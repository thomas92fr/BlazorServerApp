using CommunityToolkit.Mvvm.Input;

namespace BlazorServerApp.ViewModel.Commons.Fields
{
    public interface ICommandViewModelBase
    {
        string Text { get; set; }
        string Hint { get; set; }
        bool IsBusy { get; set; }
        bool IsEnabled { get; set; }
        CommandStyle Style { get; set; }
    }
    public interface ICommandViewModel<T> : ICommandViewModelBase
    {
        IRelayCommand<T> Command { get; }

    }

    public interface ICommandViewModel : ICommandViewModelBase
    {
        IRelayCommand Command { get; }
    }
}

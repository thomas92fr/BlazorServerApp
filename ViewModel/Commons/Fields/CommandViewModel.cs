using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ViewModel.Commons.Fields
{
    public abstract partial class CommandViewModelBase : ObservableObject, ICommandViewModelBase
    {
        private string _text = string.Empty;
        private string _hint = string.Empty;
        private bool _isEnabled = true;
        private bool _isBusy;
        private CommandStyle _style = CommandStyle.Default;

        public object? Parent { get; protected set; }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public string Hint
        {
            get => _hint;
            set => SetProperty(ref _hint, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public CommandStyle Style
        {
            get => _style;
            set => SetProperty(ref _style, value);
        }
    }

    public partial class CommandViewModel : CommandViewModelBase, ICommandViewModel
    {

        public IRelayCommand Command { get; }

        public CommandViewModel(object? parent = null, string? text = null, string? hint = null, Action? execute = null, Func<Task>? executeAsync = null, Func<bool>? canExecute = null, CommandStyle style = CommandStyle.Default)
        {
            Parent = parent;
            Text = text ?? string.Empty;
            Hint = hint ?? string.Empty;
            Style = style;

            // Create AsyncRelayCommand if executeAsync is provided, otherwise RelayCommand
            if (executeAsync != null)
            {
                Command = new AsyncRelayCommand(ExecuteWrapperAsync, () => IsEnabled && (canExecute?.Invoke() ?? true));

                async Task ExecuteWrapperAsync()
                {
                    IsBusy = true;
                    try
                    {
                        await executeAsync.Invoke();
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
            else
            {
                Command = new RelayCommand(ExecuteWrapper, () => IsEnabled && (canExecute?.Invoke() ?? true));

                void ExecuteWrapper()
                {
                    IsBusy = true;
                    try
                    {
                        execute?.Invoke();
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }
    }

    public partial class CommandViewModel<T> : CommandViewModelBase, ICommandViewModel<T>
    {

        public IRelayCommand<T> Command { get; }

        public CommandViewModel(object? parent = null, string? text = null, string? hint = null, Action<T>? execute = null, Func<T, Task>? executeAsync = null, Func<T, bool>? canExecute = null, CommandStyle style = CommandStyle.Default)
        {
            Parent = parent;
            Text = text ?? string.Empty;
            Hint = hint ?? string.Empty;
            Style = style;

            // Create AsyncRelayCommand<T> if executeAsync is provided, otherwise RelayCommand<T>
            if (executeAsync != null)
            {
                Command = new AsyncRelayCommand<T>(
                    ExecuteWrapperAsync,
                    (param) => IsEnabled && (canExecute == null || canExecute.Invoke(param!))
                );

                async Task ExecuteWrapperAsync(T? param)
                {
                    IsBusy = true;
                    try
                    {
                        await executeAsync.Invoke(param!);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
            else
            {
                Command = new RelayCommand<T>(
                    ExecuteWrapper,
                    (param) => IsEnabled && (canExecute == null || canExecute.Invoke(param!))
                );

                void ExecuteWrapper(T? param)
                {
                    IsBusy = true;
                    try
                    {
                        execute?.Invoke(param!);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }
    }
}

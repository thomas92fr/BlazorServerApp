using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ViewModel
{
    public partial class CounterViewModel : ObservableObject
    {
        [ObservableProperty]
        private int currentCount;

        [RelayCommand]
        private void IncrementCount()
        {
            CurrentCount++;
        }
    }
}

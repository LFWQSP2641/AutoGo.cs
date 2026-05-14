namespace AutoGo.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public GoPlayViewModel GoPlay { get; } = new();
}

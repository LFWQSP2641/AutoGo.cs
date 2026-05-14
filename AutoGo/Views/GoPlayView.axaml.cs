using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;

namespace AutoGo.Views;

public partial class GoPlayView : UserControl
{
    public GoPlayView()
    {
        InitializeComponent();

        LogScrollViewer.GetObservable(ScrollViewer.ExtentProperty).Subscribe(new AnonymousObserver<Size>(_ =>
        {
            LogScrollViewer.ScrollToEnd();
        }));
    }
}

using Avalonia.Controls;
using StudentDesktop.ViewModels;

namespace StudentDesktop.Views;

public partial class BrowserView : UserControl
{
    public BrowserView()
    {
        InitializeComponent();
        WebViewControl.NavigationStarted += OnNavigationStarted;
        DataContextChanged += (_, _) => WireViewModel();
    }

    private void WireViewModel()
    {
        if (DataContext is not BrowserViewModel vm)
        {
            return;
        }

        // SDA-08: the ViewModel stays UI-agnostic — it calls back into the actual
        // WebView only through these delegates, wired here rather than the ViewModel
        // holding a reference to an Avalonia control.
        vm.GetPageTitleAsync = () => WebViewControl.InvokeScript("document.title");
        vm.GetSelectedTextAsync = () => WebViewControl.InvokeScript("window.getSelection().toString()");
    }

    // SDA-03/SDA-04: the whitelist check must gate every navigation the WebView itself
    // initiates (link clicks inside the page, redirects), not just the ones the URL bar's
    // Navigate command triggers.
    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (DataContext is BrowserViewModel vm && (e.Request is null || !vm.IsWhitelisted(e.Request)))
        {
            e.Cancel = true;
        }
    }
}

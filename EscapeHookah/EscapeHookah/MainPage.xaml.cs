using System.Diagnostics;

namespace EscapeHookah
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

#if DEBUG && WINDOWS
            // Try to open WebView2 DevTools automatically to inspect console errors when running on Windows.
            try
            {
                blazorWebView.HandlerChanged += async (s, e) =>
                {
                    try
                    {
                        var platformView = blazorWebView.Handler?.PlatformView;
                        if (platformView != null)
                        {
                            var webView2 = platformView as global::Microsoft.UI.Xaml.Controls.WebView2;
                            if (webView2 != null)
                            {
                                // Ensure CoreWebView2 is initialized and open devtools
                                if (webView2.CoreWebView2 == null)
                                {
                                    await webView2.EnsureCoreWebView2Async(null);
                                }

                                webView2.CoreWebView2?.OpenDevToolsWindow();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not open WebView2 DevTools: {ex}");
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevTools attach error: {ex}");
            }
#endif
        }
    }
}

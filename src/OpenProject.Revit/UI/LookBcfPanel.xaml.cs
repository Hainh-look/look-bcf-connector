using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenProject.Revit.Entry;
using OpenProject.Revit.Services;
using OpenProject.Shared;
using OpenProject.Shared.BcfApi;
using Serilog;

namespace OpenProject.Revit.UI
{
  public partial class LookBcfPanel : UserControl
  {
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    private const string DefaultServerUrl = "https://bim.lookbim.com";
    private string _currentServerUrl = DefaultServerUrl;
    private WebView2 _webView;
    private bool _isInitialized;
    private bool _isInitializing;

    public LookBcfPanel()
    {
      InitializeComponent();
      Loaded += OnLoaded;
      IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
      TryInitializeWhenReady();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if ((bool)e.NewValue)
      {
        TryInitializeWhenReady();
      }
    }

    private void TryInitializeWhenReady()
    {
      if (_isInitialized || _isInitializing || !IsVisible) return;

      Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(async () =>
      {
        if (_isInitialized || _isInitializing || !IsVisible) return;
        await InitializeWebViewAsync();
      }));
    }

    private async Task InitializeWebViewAsync()
    {
      if (_isInitialized || _isInitializing) return;
      _isInitializing = true;

      try
      {
        // 1. Verify valid HWND parent exists
        var source = PresentationSource.FromVisual(this) as HwndSource;
        if (source == null || source.Handle == IntPtr.Zero || !IsVisible)
        {
          _isInitializing = false;
          return;
        }

        LoadingOverlay.Visibility = Visibility.Visible;
        ErrorOverlay.Visibility = Visibility.Collapsed;

        // 2. Set WebView2Loader search path to add-in directory
        var addinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(addinDir))
        {
          var loaderPath = Path.Combine(addinDir, "WebView2Loader.dll");
          if (File.Exists(loaderPath))
          {
            LoadLibrary(loaderPath);
          }
          try
          {
            CoreWebView2Environment.SetLoaderDllFolderPath(addinDir);
          }
          catch
          {
            // Ignore if already set
          }
        }

        // 3. Verify if WebView2 Runtime is installed on the machine
        bool isRuntimeAvailable = false;
        try
        {
          var runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
          isRuntimeAvailable = !string.IsNullOrEmpty(runtimeVersion);
        }
        catch
        {
          isRuntimeAvailable = false;
        }

        if (!isRuntimeAvailable)
        {
          LoadingOverlay.Visibility = Visibility.Collapsed;
          ErrorOverlay.Visibility = Visibility.Visible;
          ErrorTitleText.Text = "Chưa cài đặt Microsoft Edge WebView2";
          ErrorMessageText.Text = "Look BCF cần Microsoft Edge WebView2 Runtime để kết nối máy chủ BIM. Vui lòng bấm nút bên dưới để tải bộ cài chính thức từ Microsoft (miễn phí, ~2 MB).";
          BtnDownloadWebView2.Visibility = Visibility.Visible;
          _isInitializing = false;
          return;
        }

        // 4. User Data Folder in LocalAppData (prevents roaming/network lockups)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userDataFolder = Path.Combine(localAppData, "LookBcf", "WebView2Profile");
        Directory.CreateDirectory(userDataFolder);

        // 4. Create and attach WebView2 control dynamically
        if (_webView == null)
        {
          _webView = new WebView2();
          WebViewContainer.Children.Clear();
          WebViewContainer.Children.Add(_webView);
        }

        // 5. Initialize environment
        var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _webView.EnsureCoreWebView2Async(environment);

        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

        // Bridge script injection into every document created
        const string bridgeScript = @"
(function() {
    window.RevitBridge = {
        sendMessageToRevit: function(messageType, trackingId, messagePayload) {
            window.chrome.webview.postMessage(JSON.stringify({
                messageType: messageType,
                trackingId: trackingId,
                messagePayload: messagePayload
            }));
        },
        sendMessageToOpenProject: function(message) {
            console.log('[Look BCF] Received from Revit:', message);
        }
    };
    window.dispatchEvent(new Event('revit.plugin.ready'));
})();
";
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeScript);

        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _webView.CoreWebView2.NavigationCompleted += (s, e) =>
        {
          LoadingOverlay.Visibility = Visibility.Collapsed;
        };

        _isInitialized = true;
        NavigateToUrl(_currentServerUrl);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to initialize WebView2 in Look BCF Panel");
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Visible;
        ErrorMessageText.Text = ex.Message;

        // Clean up broken control to prevent layout crash
        if (_webView != null)
        {
          try
          {
            WebViewContainer.Children.Clear();
            _webView.Dispose();
          }
          catch { }
          _webView = null;
        }
      }
      finally
      {
        _isInitializing = false;
      }
    }

    private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
      try
      {
        var messageString = e.TryGetWebMessageAsString();
        if (string.IsNullOrEmpty(messageString)) return;

        var messageJson = JObject.Parse(messageString);
        var messageType = messageJson["messageType"]?.ToString();
        var trackingId = messageJson["trackingId"]?.ToString();
        var messagePayload = messageJson["messagePayload"]?.ToString();

        Log.Information("Received Web Message: {messageType} (Tracking: {trackingId})", messageType, trackingId);

        switch (messageType)
        {
          case MessageTypes.VIEWPOINT_DATA:
            HandleShowViewpoint(messagePayload, messageType, trackingId);
            break;

          case MessageTypes.VIEWPOINT_GENERATION_REQUESTED:
            HandleViewpointGenerationRequested(trackingId);
            break;

          case MessageTypes.GO_TO_SETTINGS:
            SettingsFlyout.Visibility = Visibility.Visible;
            break;
        }
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Error processing message from WebView2");
      }
    }

    private void HandleShowViewpoint(string payload, string messageType, string trackingId)
    {
      try
      {
        BcfViewpointWrapper bcfViewpoint = MessageDeserializer.DeserializeBcfViewpoint(
          new WebUiMessageEventArgs(messageType, trackingId, payload));
        OpenViewpointEventHandler.ShowBcfViewpoint(bcfViewpoint);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to open BCF viewpoint");
        MessageHandler.ShowError(ex, "Lỗi khi hiển thị Viewpoint trong Revit.");
      }
    }

    private void HandleViewpointGenerationRequested(string trackingId)
    {
      CreateViewpointEventHandler.CaptureAndSendViewpoint(trackingId, (tid, viewpointJson) =>
      {
        SendMessageToOpenProject(MessageTypes.VIEWPOINT_GENERATED, tid, viewpointJson);
      });
    }

    public void SendMessageToOpenProject(string messageType, string trackingId, string messagePayload)
    {
      Dispatcher.Invoke(async () =>
      {
        try
        {
          if (_webView?.CoreWebView2 == null) return;
          var messageData = JsonConvert.SerializeObject(new { messageType, trackingId, messagePayload });
          var encodedMessage = JsonConvert.ToString(messageData);
          var script = $"if (window.RevitBridge && window.RevitBridge.sendMessageToOpenProject) {{ window.RevitBridge.sendMessageToOpenProject({encodedMessage}); }}";
          await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
          Log.Error(ex, "Failed to send message to OpenProject web page");
        }
      });
    }

    public void NavigateToUrl(string url)
    {
      if (string.IsNullOrWhiteSpace(url)) return;
      if (!url.StartsWith("http://") && !url.StartsWith("https://"))
      {
        url = "https://" + url;
      }
      _currentServerUrl = url;
      try
      {
        ServerUrlText.Text = new Uri(url).Host;
        ServerUrlInput.Text = url;
      }
      catch { }

      if (_webView != null && _isInitialized)
      {
        LoadingOverlay.Visibility = Visibility.Visible;
        _webView.Source = new Uri(url);
      }
    }

    private void BtnHome_Click(object sender, RoutedEventArgs e)
    {
      NavigateToUrl(_currentServerUrl);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
      _webView?.Reload();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        SettingsFlyout.Visibility = SettingsFlyout.Visibility == Visibility.Visible 
          ? Visibility.Collapsed 
          : Visibility.Visible;
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Error toggling settings flyout");
      }
    }

    private void BtnSaveServer_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        var newUrl = ServerUrlInput.Text.Trim();
        SettingsFlyout.Visibility = Visibility.Collapsed;
        NavigateToUrl(newUrl);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Error saving server URL");
      }
    }

    private void BtnDownloadWebView2_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703")
        {
          UseShellExecute = true
        });
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to launch WebView2 installer download URL");
      }
    }

    private async void BtnRetry_Click(object sender, RoutedEventArgs e)
    {
      ErrorOverlay.Visibility = Visibility.Collapsed;
      BtnDownloadWebView2.Visibility = Visibility.Collapsed;
      await InitializeWebViewAsync();
    }
  }
}

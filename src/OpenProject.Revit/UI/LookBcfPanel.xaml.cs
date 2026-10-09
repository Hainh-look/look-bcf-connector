using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
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
    private const string DefaultServerUrl = "https://bim.lookbim.com";
    private string _currentServerUrl = DefaultServerUrl;
    private bool _isInitialized;

    public LookBcfPanel()
    {
      InitializeComponent();
      Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
      if (_isInitialized) return;
      _isInitialized = true;
      await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
      try
      {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userDataFolder = Path.Combine(localAppData, "LookBcf", "WebView2Profile");
        Directory.CreateDirectory(userDataFolder);

        var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await WebViewControl.EnsureCoreWebView2Async(environment);

        WebViewControl.CoreWebView2.Settings.IsStatusBarEnabled = false;
        WebViewControl.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        WebViewControl.CoreWebView2.Settings.AreDevToolsEnabled = true;

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
        await WebViewControl.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeScript);

        WebViewControl.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        WebViewControl.CoreWebView2.NavigationCompleted += (s, e) =>
        {
          LoadingOverlay.Visibility = Visibility.Collapsed;
        };

        NavigateToUrl(_currentServerUrl);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to initialize WebView2 in Look BCF Panel");
        LoadingOverlay.Visibility = Visibility.Collapsed;
        MessageHandler.ShowError(ex, "Không thể khởi tạo Microsoft Edge WebView2. Vui lòng đảm bảo WebView2 Runtime đã được cài đặt.");
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
          if (WebViewControl.CoreWebView2 == null) return;
          var messageData = JsonConvert.SerializeObject(new { messageType, trackingId, messagePayload });
          var encodedMessage = JsonConvert.ToString(messageData);
          var script = $"if (window.RevitBridge && window.RevitBridge.sendMessageToOpenProject) {{ window.RevitBridge.sendMessageToOpenProject({encodedMessage}); }}";
          await WebViewControl.CoreWebView2.ExecuteScriptAsync(script);
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
      ServerUrlText.Text = new Uri(url).Host;
      ServerUrlInput.Text = url;
      LoadingOverlay.Visibility = Visibility.Visible;
      WebViewControl.Source = new Uri(url);
    }

    private void BtnHome_Click(object sender, RoutedEventArgs e)
    {
      NavigateToUrl(_currentServerUrl);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
      WebViewControl.Reload();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
      SettingsFlyout.Visibility = SettingsFlyout.Visibility == Visibility.Visible 
        ? Visibility.Collapsed 
        : Visibility.Visible;
    }

    private void BtnSaveServer_Click(object sender, RoutedEventArgs e)
    {
      var newUrl = ServerUrlInput.Text.Trim();
      SettingsFlyout.Visibility = Visibility.Collapsed;
      NavigateToUrl(newUrl);
    }
  }
}

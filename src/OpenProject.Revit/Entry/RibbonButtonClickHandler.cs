using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using OpenProject.Revit.Services;
using OpenProject.Shared;
using Serilog;
using ZetaIpc.Runtime.Helper;

namespace OpenProject.Revit.Entry
{
  public static class RibbonButtonClickHandler
  {
#if RevitNetCore
    public const string RevitVersion = "2025+";
#elif Version2024 || RevitNetFramework
    public const string RevitVersion = "2021-2024";
#elif Version2022
    public const string RevitVersion = "2022";
#elif Version2021
    public const string RevitVersion = "2021";
#elif Version2020
    public const string RevitVersion = "2020";
#elif Version2019
    public const string RevitVersion = "2019";
#else
    public const string RevitVersion = "2021-2026";
#endif

    private static Process _opBrowserProcess;
    public static IpcHandler IpcHandler { get; private set; }

    public static Result OpenMainPluginWindow(ExternalCommandData commandData, ref string message)
    {
      try
      {
        EnsureExternalOpenProjectAppIsRunning(commandData);
        IpcHandler.SendBringBrowserToForegroundRequestToDesktopApp();

        return Result.Succeeded;
      }
      catch (Exception exception)
      {
        message = exception.Message;
        Log.Error(exception, message);
        return Result.Failed;
      }
    }

    public static Result OpenSettingsPluginWindow(ExternalCommandData commandData, ref string message)
    {
      try
      {
        EnsureExternalOpenProjectAppIsRunning(commandData);
        IpcHandler.SendOpenSettingsRequestToDesktopApp();
        IpcHandler.SendBringBrowserToForegroundRequestToDesktopApp();
        return Result.Succeeded;
      }
      catch (Exception exception)
      {
        message = exception.Message;
        Log.Error(exception, message);
        return Result.Failed;
      }
    }

    private static void EnsureExternalOpenProjectAppIsRunning(ExternalCommandData commandData)
    {
      // Version check
      var versionName = commandData.Application.Application.VersionName;
#if RevitNetCore
      var isSupported = versionName.Contains("2025") || versionName.Contains("2026") || versionName.Contains("2027");
#else
      var isSupported = versionName.Contains("2021") || versionName.Contains("2022") || versionName.Contains("2023") || versionName.Contains("2024");
#endif
      if (!isSupported)
      {
        MessageHandler.ShowWarning(
          "Unexpected version",
          "The Revit version does not match the expectations.",
          $"This build was prepared for Revit {RevitVersion} (running on {versionName}). Further usage is at your own risk.");
      }

      if (_opBrowserProcess is { HasExited: false })
        return;

      IpcHandler = new IpcHandler(commandData.Application);
      var revitServerPort = IpcHandler.StartLocalServerAndReturnPort();

      var openProjectBrowserExecutablePath = GetOpenProjectBrowserExecutable();
      if (!File.Exists(openProjectBrowserExecutablePath))
        throw new SystemException("Browser executable not found.");

      var opBrowserServerPort = FreePortHelper.GetFreePort();
      var processArguments = $"ipc {opBrowserServerPort} {revitServerPort}";
      _opBrowserProcess = Process.Start(openProjectBrowserExecutablePath, processArguments);
      IpcHandler.StartLocalClient(opBrowserServerPort);
      Log.Information("IPC bridge started between port {port1} and {port2}.",
        opBrowserServerPort, revitServerPort);
    }

    private static string GetOpenProjectBrowserExecutable()
    {
      var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
      var currentFolder = Path.GetDirectoryName(currentAssemblyPath) ?? string.Empty;

      return Path.Combine(currentFolder, ConfigurationConstant.OpenProjectBrowserExecutablePath);
    }
  }
}

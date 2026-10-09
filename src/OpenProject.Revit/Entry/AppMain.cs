using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using OpenProject.Revit.UI;
using OpenProject.Shared.Logging;

namespace OpenProject.Revit.Entry
{
  [Transaction(TransactionMode.Manual)]
  public class AppMain : IExternalApplication
  {
    public static readonly DockablePaneId LookBcfPaneId = new DockablePaneId(new Guid("7D9E0A4E-4394-4328-9844-4F8177F8DC90"));
    private readonly string _path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    #region Revit IExternalApplication Implementation

    public Result OnStartup(UIControlledApplication application)
    {
      try
      {
        Logger.ConfigureLogger("LookBcf.Revit.Log..txt");

        // 1. Register Dockable Pane
        var paneProvider = new LookBcfDockablePaneProvider();
        application.RegisterDockablePane(LookBcfPaneId, "Look BCF", paneProvider);

        // 2. Setup Ribbon Tab & Panel
        const string tabName = "Look BCF";
        const string panelName = "BCF Management";
        
        try
        {
          application.CreateRibbonTab(tabName);
        }
        catch
        {
          // Tab might already exist
        }

        RibbonPanel panel = application.CreateRibbonPanel(tabName, panelName);

        // 3. Add Look BCF Dockable Panel Button
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        RibbonItem bcfButtonItem = panel.AddItem(
          new PushButtonData("LookBcfToggle",
            "Look BCF",
            assemblyLocation,
            "OpenProject.Revit.Entry.CmdMain"));

        if (bcfButtonItem is PushButton bcfButton)
        {
          bcfButton.Image = LoadPngImgSource("OpenProject.Revit.Assets.OpenProjectLogo16.png");
          bcfButton.LargeImage = LoadPngImgSource("OpenProject.Revit.Assets.OpenProjectLogo32.png");
          bcfButton.ToolTip = "Mở bảng kết nối Look BCF (OpenProject BIM)";
        }
      }
      catch (Exception exception)
      {
        MessageBox.Show("Exception on Look BCF startup: " + exception, "Look BCF Error");
        return Result.Failed;
      }

      return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
      return Result.Succeeded;
    }

    #endregion

    #region Private Members

    private ImageSource LoadPngImgSource(string resourceName)
    {
      try
      {
        var assembly = typeof(AppMain).Assembly;
        var icon = assembly.GetManifestResourceStream(resourceName);
        if (icon == null) return null;

        PngBitmapDecoder decoder =
          new PngBitmapDecoder(icon, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);

        return decoder.Frames[0];
      }
      catch
      {
        return null;
      }
    }

    #endregion
  }
}

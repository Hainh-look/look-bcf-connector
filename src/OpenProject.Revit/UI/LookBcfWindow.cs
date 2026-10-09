using System;
using System.Windows;
using Autodesk.Revit.UI;

namespace OpenProject.Revit.UI
{
  public class LookBcfWindow : Window
  {
    private static LookBcfWindow _instance;

    public LookBcfWindow()
    {
      Title = "Look BCF - Lookspace BIM";
      Width = 440;
      Height = 740;
      MinWidth = 360;
      MinHeight = 500;
      WindowStartupLocation = WindowStartupLocation.CenterScreen;
      Content = new LookBcfPanel();
      Closed += (s, e) => _instance = null;
    }

    public static void ShowFloating(UIApplication uiApp)
    {
      if (_instance == null)
      {
        _instance = new LookBcfWindow();
        try
        {
          var revitWindowHandle = uiApp.MainWindowHandle;
          if (revitWindowHandle != IntPtr.Zero)
          {
            new System.Windows.Interop.WindowInteropHelper(_instance).Owner = revitWindowHandle;
          }
        }
        catch
        {
          // Ignore handle assignment failure
        }
        _instance.Show();
      }
      else
      {
        if (_instance.WindowState == WindowState.Minimized)
        {
          _instance.WindowState = WindowState.Normal;
        }
        _instance.Activate();
      }
    }
  }
}

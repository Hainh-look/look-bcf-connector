using System;
using Autodesk.Revit.UI;

namespace OpenProject.Revit.UI
{
  public class LookBcfDockablePaneProvider : IDockablePaneProvider
  {
    private static LookBcfPanel _panel;
    public static LookBcfPanel Panel => _panel ??= new LookBcfPanel();

    public void SetupDockablePane(DockablePaneProviderData data)
    {
      data.FrameworkElement = Panel;
      data.InitialState = new DockablePaneState
      {
        DockPosition = DockPosition.Right
      };
    }
  }
}

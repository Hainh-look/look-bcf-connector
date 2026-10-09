using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OpenProject.Revit.Entry
{
  [Transaction(TransactionMode.Manual)]
  [Regeneration(RegenerationOption.Manual)]
  public class CmdMain : IExternalCommand
  {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet _)
    {
      try
      {
        DockablePane pane = commandData.Application.GetDockablePane(AppMain.LookBcfPaneId);
        if (pane != null)
        {
          if (pane.IsShown())
          {
            pane.Hide();
          }
          else
          {
            pane.Show();
          }
        }
        return Result.Succeeded;
      }
      catch (Exception ex)
      {
        message = ex.Message;
        return Result.Failed;
      }
    }
  }
}

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenProject.Revit.UI;
using Serilog;

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
        DockablePane pane = null;
        try
        {
          pane = commandData.Application.GetDockablePane(AppMain.LookBcfPaneId);
        }
        catch (Exception ex)
        {
          // Dockable pane has not been created yet
          // This occurs when executing via AddInManager or if Revit was not restarted after installing .addin
          Log.Information("Dockable pane not yet created ({msg}). Switching to floating window fallback.", ex.Message);
        }

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
        else
        {
          // Fallback: Open as Floating Window so users can test immediately!
          LookBcfWindow.ShowFloating(commandData.Application);
        }

        return Result.Succeeded;
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to toggle Look BCF window");
        message = ex.Message;
        return Result.Failed;
      }
    }
  }
}

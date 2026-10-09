using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using iabi.BCF.APIObjects.V21;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OpenProject.Revit.Extensions;
using OpenProject.Revit.Services;
using OpenProject.Shared;
using Serilog;

namespace OpenProject.Revit.Entry
{
  public class CreateViewpointEventHandler : IExternalEventHandler
  {
    private static CreateViewpointEventHandler _instance;
    private static ExternalEvent _externalEvent;
    private string _pendingTrackingId;
    private Action<string, string> _onViewpointGenerated;

    private static CreateViewpointEventHandler Instance
    {
      get
      {
        if (_instance != null) return _instance;
        _instance = new CreateViewpointEventHandler();
        _externalEvent = ExternalEvent.Create(_instance);
        return _instance;
      }
    }

    public static void CaptureAndSendViewpoint(string trackingId, Action<string, string> onViewpointGenerated)
    {
      Instance._pendingTrackingId = trackingId;
      Instance._onViewpointGenerated = onViewpointGenerated;
      _externalEvent.Raise();
    }

    public void Execute(UIApplication app)
    {
      try
      {
        if (app.ActiveUIDocument == null || app.ActiveUIDocument.ActiveView == null)
        {
          MessageHandler.ShowWarning("No Active Document", "Document not found", "Please open a project and a 3D view in Revit.");
          return;
        }

        if (app.ActiveUIDocument.ActiveView.ViewType != ViewType.ThreeD)
        {
          MessageHandler.ShowWarning(
            "Invalid View",
            "Active UI document is not a 3D view",
            "In order to capture BCF viewpoints, Look BCF requires an active 3D view.");
          return;
        }

        var payload = GenerateJsonViewpoint(app.ActiveUIDocument);
        payload["snapshot"] = payload["snapshot"]?["snapshot_data"];

        var trackingId = _pendingTrackingId ?? "0";
        var payloadString = payload.ToString();
        Log.Information("Viewpoint generated successfully for tracking ID {trackingId}", trackingId);
        _onViewpointGenerated?.Invoke(trackingId, payloadString);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to capture BCF viewpoint from active Revit view");
        MessageHandler.ShowError(ex, "Error capturing viewpoint from Revit.");
      }
    }

    public string GetName() => nameof(CreateViewpointEventHandler);

    private static JObject GenerateJsonViewpoint(UIDocument uiDocument)
    {
      var serializerSettings = new JsonSerializerSettings
      {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
      };
      serializerSettings.Converters.Add(new StringEnumConverter(new SnakeCaseNamingStrategy(), false));

      Viewpoint_POST viewpoint = uiDocument.GenerateViewpoint();
      return JObject.Parse(JsonConvert.SerializeObject(viewpoint, serializerSettings));
    }
  }
}

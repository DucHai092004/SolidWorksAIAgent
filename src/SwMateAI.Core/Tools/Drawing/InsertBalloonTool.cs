using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Drawing;

namespace SwMateAI.Core.Tools.Drawing
{
    public class InsertBalloonTool : SwToolBase
    {
        public InsertBalloonTool(ISldWorks swApp) : base(swApp) { }
        public override string Name => "InsertBalloon";
        public override string Description => "Automatically inserts BOM item balloons around a drawing view.";

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            { reason = "Open a Drawing before inserting balloons."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                var model = SwApp.ActiveDoc as IModelDoc2;
                var drawing = model as IDrawingDoc;
                var view = (drawing?.GetFirstView() as IView)?.GetNextView() as IView;
                if (view == null) return ToolResult.Error("No model drawing view was found.");

                model.ClearSelection2(true);
                if (!model.Extension.SelectByID2(view.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0))
                    return ToolResult.Error("Could not select the drawing view.");

                var options = drawing.CreateAutoBalloonOptions() as AutoBalloonOptions;
                if (options == null) return ToolResult.Error("SOLIDWORKS did not provide auto-balloon options.");
                options.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Square;
                options.Style = (int)swBalloonStyle_e.swBS_Circular;
                options.Size = (int)swBalloonFit_e.swBF_Tightest;
                options.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber;
                options.ItemOrder = (int)swBalloonItemNumbersOrder_e.swBalloonItemNumbers_DoNotChangeItemNumbers;
                options.IgnoreMultiple = true;
                options.InsertMagneticLine = false;

                object raw = drawing.AutoBalloon5(options);
                var notes = raw as object[];
                int count = notes?.Length ?? 0;
                model.ClearSelection2(true);
                model.EditRebuild3();
                if (count == 0) return ToolResult.Error("No balloons were created. Ensure the drawing has a BOM and the view contains BOM items.");

                return ToolResult.Success(new DrawingBalloonResult { ViewName = view.GetName2(), BalloonCount = count });
            }
            catch (Exception ex) { return ToolResult.Error("InsertBalloon failed: " + ex.Message); }
        }
    }
}

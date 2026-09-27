using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    /// <summary>
    /// Composite CAD workflow: creates a rectangular plate and a centered blind hole
    /// from one command. Dimensions are in millimeters.
    /// </summary>
    public class CreatePlateWithHoleTool : SwToolBase
    {
        public override string Name => "CreatePlateWithHole";
        public override string Description => "Creates a new rectangular plate with a centered circular blind hole.";
        public CreatePlateWithHoleTool(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var template = SwApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            if (string.IsNullOrWhiteSpace(template))
            {
                reason = "No default Part template is configured in SOLIDWORKS.";
                return false;
            }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                double width = Positive(parameters, "Width", 100);
                double height = Positive(parameters, "Height", 60);
                double thickness = Positive(parameters, "Thickness", 10);
                double holeDiameter = Positive(parameters, "HoleDiameter", 10);
                double holeDepth = Positive(parameters, "HoleDepth", thickness);
                double holeX = Number(parameters, "HoleX", 0);
                double holeY = Number(parameters, "HoleY", 0);

                if (holeDiameter >= Math.Min(width, height))
                    return ToolResult.Error("Hole diameter must be smaller than the plate width and height.");

                string template = SwApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                var model = SwApp.NewDocument(template, 0, 0, 0) as IModelDoc2;
                if (model == null) return ToolResult.Error("Could not create the Part document.");

                // 1) First reference plane -> centered rectangle.
                IFeature plane = model.FirstFeature() as IFeature;
                while (plane != null && plane.GetTypeName2() != "RefPlane")
                    plane = plane.GetNextFeature() as IFeature;
                if (plane == null) return ToolResult.Error("No reference plane was found.");

                model.ClearSelection2(true);
                if (!plane.Select2(false, 0)) return ToolResult.Error("Could not select the reference plane.");
                model.SketchManager.InsertSketch(true);

                double halfW = width / 2000.0;
                double halfH = height / 2000.0;
                if (model.SketchManager.CreateCornerRectangle(-halfW, -halfH, 0, halfW, halfH, 0) == null)
                    return ToolResult.Error("Could not create the plate rectangle.");

                model.SketchManager.InsertSketch(true);
                var boss = model.FeatureManager.FeatureExtrusion2(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                    thickness / 1000.0, 0.0,
                    false, false, false, false, 0.0, 0.0,
                    false, false, false, false, true, true, true,
                    (int)swStartConditions_e.swStartSketchPlane, 0.0, false);
                if (boss == null) return ToolResult.Error("Could not create the plate extrusion.");

                // 2) Select the largest planar face of the resulting body.
                var part = model as IPartDoc;
                if (part == null) return ToolResult.Error("The active document is not a valid Part document.");
                object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                if (bodies == null || bodies.Length == 0) return ToolResult.Error("No solid body was found after extrusion.");

                IFace2 bestFace = null;
                double bestArea = -1.0;
                foreach (object bodyObj in bodies)
                {
                    var body = bodyObj as IBody2;
                    object[] faces = body?.GetFaces() as object[];
                    if (faces == null) continue;
                    foreach (object faceObj in faces)
                    {
                        var face = faceObj as IFace2;
                        if (face == null) continue;
                        var surface = face.GetSurface() as ISurface;
                        if (surface == null || !surface.IsPlane()) continue;
                        double area = face.GetArea();
                        if (area > bestArea) { bestArea = area; bestFace = face; }
                    }
                }
                if (bestFace == null) return ToolResult.Error("Could not find a planar face for the hole sketch.");

                model.ClearSelection2(true);
                var faceEntity = bestFace as IEntity;
                if (faceEntity == null || !faceEntity.Select4(false, null))
                    return ToolResult.Error("Could not select the plate face.");
                model.SketchManager.InsertSketch(true);
                if (model.SketchManager.ActiveSketch == null) return ToolResult.Error("Could not create the hole sketch.");

                double radius = holeDiameter / 2000.0;
                if (model.SketchManager.CreateCircleByRadius(holeX / 1000.0, holeY / 1000.0, 0, radius) == null)
                    return ToolResult.Error("Could not create the hole circle.");

                model.SketchManager.InsertSketch(true);
                var cut = model.FeatureManager.FeatureCut3(
                    true, false, false,
                    (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                    holeDepth / 1000.0, 0.0,
                    false, false, false, false, 0.0, 0.0,
                    false, false, false, false, false, true, true, true, true,
                    false, 0, 0.0, false);
                if (cut == null) return ToolResult.Error("Could not create the hole Cut-Extrude.");

                model.ViewZoomtofit2();
                return ToolResult.Success($"Plate created: {width:0.###} x {height:0.###} x {thickness:0.###} mm, hole Ø{holeDiameter:0.###} mm at X={holeX:0.###}, Y={holeY:0.###}, cut depth {holeDepth:0.###} mm.");
            }
            catch (Exception ex) { return ToolResult.Error($"CreatePlateWithHole failed: {ex.Message}"); }
        }

        private static double Number(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            return Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double Positive(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            double value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (value <= 0) throw new ArgumentOutOfRangeException(key, "Value must be greater than zero.");
            return value;
        }
    }
}
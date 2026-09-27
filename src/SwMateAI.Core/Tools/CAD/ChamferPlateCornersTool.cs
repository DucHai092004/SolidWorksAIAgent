using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwMateAI.Core.Tools.CAD
{
    public class ChamferPlateCornersTool : SwToolBase
    {
        public override string Name => "ChamferPlateCorners";
        public override string Description => "Chamfers the four vertical corner edges of the active plate.";
        public ChamferPlateCornersTool(ISldWorks swApp) : base(swApp) { }

        public override bool CanExecute(out string reason)
        {
            reason = null;
            var model = SwApp.ActiveDoc as IModelDoc2;
            if (model == null) { reason = "No active SOLIDWORKS document."; return false; }
            if (model.GetType() != (int)swDocumentTypes_e.swDocPART) { reason = "The active document must be a Part."; return false; }
            return true;
        }

        public override ToolResult Execute(Dictionary<string, object> parameters)
        {
            try
            {
                double distanceMm = Positive(parameters, "Distance", 2);
                var model = SwApp.ActiveDoc as IModelDoc2;
                var part = model as IPartDoc;
                if (part == null) return ToolResult.Error("The active document is not a Part.");

                var edges = GetVerticalEdges(part);
                if (edges.Count != 4)
                    return ToolResult.Error($"Expected 4 vertical plate edges but found {edges.Count}.");

                model.ClearSelection2(true);
                for (int i = 0; i < edges.Count; i++)
                {
                    var entity = edges[i] as IEntity;
                    if (entity == null || !entity.Select4(i > 0, null))
                        return ToolResult.Error("Could not select all plate corner edges.");
                }

                var feature = model.FeatureManager.InsertFeatureChamfer(
                    0,
                    (int)swChamferType_e.swChamferEqualDistance,
                    distanceMm / 1000.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0);

                if (feature == null) return ToolResult.Error("SOLIDWORKS did not create the chamfer.");
                model.ViewZoomtofit2();
                return ToolResult.Success($"Chamfered 4 plate corners: {distanceMm:0.###} mm.");
            }
            catch (Exception ex) { return ToolResult.Error($"ChamferPlateCorners failed: {ex.Message}"); }
        }

        private static List<IEdge> GetVerticalEdges(IPartDoc part)
        {
            var result = new List<IEdge>();
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null) return result;

            const double tol = 1e-8;
            foreach (object bodyObj in bodies)
            {
                var body = bodyObj as IBody2;
                object[] edges = body?.GetEdges() as object[];
                if (edges == null) continue;

                foreach (object edgeObj in edges)
                {
                    var edge = edgeObj as IEdge;
                    var v1 = edge?.GetStartVertex() as IVertex;
                    var v2 = edge?.GetEndVertex() as IVertex;
                    var p1 = v1?.GetPoint() as double[];
                    var p2 = v2?.GetPoint() as double[];
                    if (p1 == null || p2 == null) continue;

                    double dx = Math.Abs(p2[0] - p1[0]);
                    double dy = Math.Abs(p2[1] - p1[1]);
                    double dz = Math.Abs(p2[2] - p1[2]);
                    if (dx < tol && dy < tol && dz > tol) result.Add(edge);
                }
            }
            return result;
        }

        private static double Positive(Dictionary<string, object> p, string key, double fallback)
        {
            if (!p.TryGetValue(key, out var raw) || raw == null) return fallback;
            double value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (value <= 0) throw new ArgumentOutOfRangeException(key);
            return value;
        }
    }
}

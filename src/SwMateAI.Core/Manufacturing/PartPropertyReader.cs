using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Common;

namespace SwMateAI.Core.Manufacturing
{
    public class PartPropertyReader
    {
        private readonly ISldWorks _swApp;
        public PartPropertyReader(ISldWorks swApp) { _swApp = swApp; }

        public BreakdownItem Read(ScannedPartOccurrence occurrence, int quantity)
        {
            var item = new BreakdownItem
            {
                Quantity = quantity,
                SourcePath = occurrence.SourcePath,
                RepresentativeComponentName = occurrence.ComponentName,
                Configuration = occurrence.ReferencedConfiguration,
                IsLoaded = occurrence.IsLoaded,
                ManufacturingTechnology = string.Empty,
                Supplier = string.Empty
            };

            var assembly = _swApp.ActiveDoc as IAssemblyDoc;
            var component = FindComponent(assembly, occurrence.ComponentName);
            var model = component?.GetModelDoc2() as IModelDoc2;
            string fallback = PartCodeNormalizer.CleanDisplay(
                !string.IsNullOrWhiteSpace(occurrence.SourcePath)
                    ? occurrence.SourcePath
                    : occurrence.ModelTitle);

            if (model == null)
            {
                item.PartNumber = fallback;
                item.PartName = fallback;
                item.Description = "Component is not loaded; properties were not read.";
                return item;
            }

            item.IsLoaded = true;
            string config = occurrence.ReferencedConfiguration;
            item.PartNumber = FirstNonEmpty(ReadProperty(model, config, "Part Number"), ReadProperty(model, config, "PartNumber"), fallback);
            item.Description = FirstNonEmpty(ReadProperty(model, config, "Description"), ReadProperty(model, config, "DESCRIPTION"));
            item.PartName = FirstNonEmpty(item.Description, item.PartNumber, fallback);

            var part = model as IPartDoc;
            if (part != null)
            {
                string database;
                item.Material = part.GetMaterialPropertyName2(config, out database) ?? string.Empty;
                ReadBoundingBox(part, item);
                item.LargestCylinderDiameterMm = GetLargestCylinderDiameterMm(part, out bool axisAligned);
                item.HasCylindricalFace = item.LargestCylinderDiameterMm > 0;
                item.LargestCylinderAxisAligned = axisAligned;
                var mass = model.Extension.CreateMassProperty() as IMassProperty;
                if (mass != null) item.DensityKgM3 = mass.Density;
            }
            return item;
        }

        private static IComponent2 FindComponent(IAssemblyDoc assembly, string name)
        {
            if (assembly == null) return null;
            var direct = assembly.GetComponentByName(name);
            if (direct != null) return direct;
            var raw = assembly.GetComponents(false) as object[];
            if (raw == null) return null;
            foreach (var obj in raw)
                if (obj is IComponent2 c && string.Equals(c.Name2, name, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        private static string ReadProperty(IModelDoc2 model, string config, string name)
        {
            string value = ReadPropertyFrom(model, config, name);
            return string.IsNullOrWhiteSpace(value) ? ReadPropertyFrom(model, string.Empty, name) : value;
        }

        private static string ReadPropertyFrom(IModelDoc2 model, string config, string name)
        {
            var manager = model?.Extension?.get_CustomPropertyManager(config ?? string.Empty);
            if (manager == null) return string.Empty;
            string raw, resolved; bool wasResolved, linked;
            manager.Get6(name, false, out raw, out resolved, out wasResolved, out linked);
            return string.IsNullOrWhiteSpace(resolved) ? raw ?? string.Empty : resolved;
        }

        private static void ReadBoundingBox(IPartDoc part, BreakdownItem item)
        {
            // GetPartBox is approximate. Extreme points query the solid geometry.
            var bodies = part?.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null || bodies.Length == 0) return;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            foreach (var bodyObj in bodies)
            {
                if (!(bodyObj is IBody2 body)) continue;
                double x, y, z;
                body.GetExtremePoint(1, 0, 0, out x, out y, out z);
                maxX = Math.Max(maxX, x);
                body.GetExtremePoint(-1, 0, 0, out x, out y, out z);
                minX = Math.Min(minX, x);
                body.GetExtremePoint(0, 1, 0, out x, out y, out z);
                maxY = Math.Max(maxY, y);
                body.GetExtremePoint(0, -1, 0, out x, out y, out z);
                minY = Math.Min(minY, y);
                body.GetExtremePoint(0, 0, 1, out x, out y, out z);
                maxZ = Math.Max(maxZ, z);
                body.GetExtremePoint(0, 0, -1, out x, out y, out z);
                minZ = Math.Min(minZ, z);
            }
            if (double.IsInfinity(minX) || double.IsInfinity(maxX)) return;
            item.FinishedXmm = Math.Abs(maxX - minX) * 1000.0;
            item.FinishedYmm = Math.Abs(maxY - minY) * 1000.0;
            item.FinishedZmm = Math.Abs(maxZ - minZ) * 1000.0;
        }

        private static double GetLargestCylinderDiameterMm(IPartDoc part, out bool axisAligned)
        {
            double largest = 0;
            axisAligned = false;
            var bodies = part?.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
            if (bodies == null) return largest;
            foreach (var bodyObj in bodies)
            {
                var faces = (bodyObj as IBody2)?.GetFaces() as object[];
                if (faces == null) continue;
                foreach (var faceObj in faces)
                {
                    var surface = (faceObj as IFace2)?.GetSurface() as ISurface;
                    if (surface == null || !surface.IsCylinder()) continue;
                    var parameters = surface.CylinderParams as Array;
                    if (parameters == null || parameters.Length < 7) continue;
                    double radiusM = Math.Abs(Convert.ToDouble(parameters.GetValue(6)));
                    double diameter = radiusM * 2000.0;
                    if (diameter <= largest) continue;
                    largest = diameter;
                    double dx = Convert.ToDouble(parameters.GetValue(3));
                    double dy = Convert.ToDouble(parameters.GetValue(4));
                    double dz = Convert.ToDouble(parameters.GetValue(5));
                    double norm = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    axisAligned = norm > 0 && Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz))) / norm >= 0.995;
                }
            }
            return largest;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values) if (!string.IsNullOrWhiteSpace(value)) return value;
            return string.Empty;
        }
    }
}

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
                IsVirtual = occurrence.IsVirtual,
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
                item.LargestCylinderDiameterMm = GetLargestCylinderDiameterMm(part);
                item.HasCylindricalFace = item.LargestCylinderDiameterMm > 0;
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
            var box = part?.GetPartBox(true) as Array;
            if (box == null || box.Length < 6) return;
            double minX = Convert.ToDouble(box.GetValue(0)), minY = Convert.ToDouble(box.GetValue(1)), minZ = Convert.ToDouble(box.GetValue(2));
            double maxX = Convert.ToDouble(box.GetValue(3)), maxY = Convert.ToDouble(box.GetValue(4)), maxZ = Convert.ToDouble(box.GetValue(5));
            item.FinishedXmm = Math.Abs(maxX - minX) * 1000.0;
            item.FinishedYmm = Math.Abs(maxY - minY) * 1000.0;
            item.FinishedZmm = Math.Abs(maxZ - minZ) * 1000.0;
        }

        private static double GetLargestCylinderDiameterMm(IPartDoc part)
        {
            double largest = 0;
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
                    largest = Math.Max(largest, radiusM * 2000.0);
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

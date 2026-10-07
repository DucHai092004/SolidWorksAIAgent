using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Manufacturing
{
    /// <summary>
    /// Expands one manufactured Part row into SOLIDWORKS cut-list stock rows when
    /// the component contains Sheet Metal or Weldment cut-list properties.
    /// Generic parts are intentionally left unchanged by the caller.
    /// </summary>
    public class CutListStockExpander
    {
        private readonly ISldWorks _swApp;

        public CutListStockExpander(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        public List<BreakdownItem> Expand(BreakdownItem baseItem)
        {
            var result = new List<BreakdownItem>();
            if (baseItem == null) return result;

            var assembly = _swApp.ActiveDoc as IAssemblyDoc;
            var component = FindComponent(assembly, baseItem.RepresentativeComponentName);
            var model = component?.GetModelDoc2() as IModelDoc2;
            if (model == null) return result;

            IFeature feature = model.FirstFeature() as IFeature;
            int cutListIndex = 0;
            while (feature != null)
            {
                if (string.Equals(feature.GetTypeName2(), "CutListFolder", StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, string> properties = ReadProperties(feature);
                    BreakdownItem expanded = CreateFromProperties(baseItem, properties, ++cutListIndex);
                    if (expanded != null) result.Add(expanded);
                }
                feature = feature.GetNextFeature() as IFeature;
            }

            return result;
        }

        /// <summary>
        /// Pure transformation used by automated tests and by the COM cut-list reader.
        /// Returns null for a generic/unrecognized cut-list property set.
        /// </summary>
        public static BreakdownItem CreateFromProperties(
            BreakdownItem baseItem,
            IDictionary<string, string> properties,
            int cutListIndex = 1)
        {
            if (baseItem == null || properties == null) return null;

            double blankLength = Number(properties,
                "Bounding Box Length", "SW-Bounding Box Length", "3D-Bounding Box Length");
            double blankWidth = Number(properties,
                "Bounding Box Width", "SW-Bounding Box Width", "3D-Bounding Box Width");
            double sheetThickness = Number(properties,
                "Sheet Metal Thickness", "SW-Sheet Metal Thickness", "Thickness", "SW-Thickness");
            int quantity = Integer(properties, "QUANTITY", "Quantity");

            if (blankLength > 0 && blankWidth > 0 && sheetThickness > 0 &&
                HasAny(properties, "Sheet Metal Thickness", "SW-Sheet Metal Thickness"))
            {
                var item = Clone(baseItem);
                item.ManufacturingForm = "Sheet Metal";
                item.FlatBlankLengthMm = blankLength;
                item.FlatBlankWidthMm = blankWidth;
                item.SheetMetalThicknessMm = sheetThickness;
                item.StockType = "Sheet Metal";
                item.StockSize = $"{blankLength:0.###} x {blankWidth:0.###} x {sheetThickness:0.###} mm";
                item.StockVolumeMm3 = blankLength * blankWidth * sheetThickness;
                item.StockClassificationBasis = "SOLIDWORKS Sheet Metal cut-list flat-pattern properties";
                item.StockSizeRule = "Exact flat-pattern bounding box; no machining allowance added";
                item.StockThicknessBasis = "SOLIDWORKS Sheet Metal Thickness";
                item.ManufacturingEvidence = "Cut-List-Item " + cutListIndex +
                    ": Bounding Box Length/Width + Sheet Metal Thickness";
                if (quantity > 0) item.Quantity = baseItem.Quantity * quantity;
                return item;
            }

            double length = Number(properties, "LENGTH", "Length", "SW-Length");
            bool hasWeldmentEvidence = HasAny(properties, "LENGTH", "ANGLE1", "ANGLE2", "Angle1", "Angle2");
            if (length > 0 && hasWeldmentEvidence)
            {
                var item = Clone(baseItem);
                item.ManufacturingForm = "Weldment";
                item.WeldmentProfileDescription = Text(properties, "Description", "DESCRIPTION", "Profile");
                item.WeldmentCutLengthMm = length;
                item.WeldmentAngle1Deg = Number(properties, "ANGLE1", "Angle1");
                item.WeldmentAngle2Deg = Number(properties, "ANGLE2", "Angle2");
                item.WeldmentCutQuantity = quantity > 0 ? quantity : 1;
                item.Quantity = baseItem.Quantity * item.WeldmentCutQuantity;
                item.StockType = "Weldment Profile";
                item.StockSize = BuildWeldmentSize(item);
                item.StockClassificationBasis = "SOLIDWORKS Weldment cut-list properties";
                item.StockSizeRule = "Exact cut-list LENGTH/ANGLE1/ANGLE2; no machining allowance added";
                item.StockThicknessBasis = "Defined by weldment profile";
                item.ManufacturingEvidence = "Cut-List-Item " + cutListIndex +
                    ": LENGTH/ANGLE1/ANGLE2/QUANTITY";
                return item;
            }

            return null;
        }

        private static string BuildWeldmentSize(BreakdownItem item)
        {
            string profile = string.IsNullOrWhiteSpace(item.WeldmentProfileDescription)
                ? "Profile"
                : item.WeldmentProfileDescription.Trim();
            return profile +
                $" | L={item.WeldmentCutLengthMm:0.###} mm" +
                $" | A1={item.WeldmentAngle1Deg:0.###}°" +
                $" | A2={item.WeldmentAngle2Deg:0.###}°";
        }

        private static Dictionary<string, string> ReadProperties(IFeature feature)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CustomPropertyManager manager = null;
            try { manager = feature?.CustomPropertyManager; } catch { }
            if (manager == null) return values;

            object rawNames = null;
            try { rawNames = manager.GetNames(); } catch { }
            if (!(rawNames is object[] names)) return values;

            foreach (object rawName in names)
            {
                string name = Convert.ToString(rawName) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;

                string raw, resolved;
                bool wasResolved, linked;
                try
                {
                    manager.Get6(name, false, out raw, out resolved, out wasResolved, out linked);
                    values[name] = string.IsNullOrWhiteSpace(resolved) ? raw ?? string.Empty : resolved;
                }
                catch { }
            }

            return values;
        }

        private static IComponent2 FindComponent(IAssemblyDoc assembly, string name)
        {
            if (assembly == null || string.IsNullOrWhiteSpace(name)) return null;
            try
            {
                IComponent2 direct = assembly.GetComponentByName(name);
                if (direct != null) return direct;
            }
            catch { }

            object[] raw = null;
            try { raw = assembly.GetComponents(false) as object[]; } catch { }
            if (raw == null) return null;
            foreach (object value in raw)
            {
                var component = value as IComponent2;
                if (component != null && string.Equals(component.Name2, name, StringComparison.OrdinalIgnoreCase))
                    return component;
            }
            return null;
        }

        private static BreakdownItem Clone(BreakdownItem source)
        {
            return new BreakdownItem
            {
                ImagePath = source.ImagePath,
                PartNumber = source.PartNumber,
                PartName = source.PartName,
                Quantity = source.Quantity,
                Material = source.Material,
                StockMaterial = source.StockMaterial,
                StockMaterialSource = source.StockMaterialSource,
                StockMaterialSourceLocation = source.StockMaterialSourceLocation,
                StockMaterialConfidence = source.StockMaterialConfidence,
                StockMatchMethod = source.StockMatchMethod,
                StockMaterialNeedsReview = source.StockMaterialNeedsReview,
                FinishedXmm = source.FinishedXmm,
                FinishedYmm = source.FinishedYmm,
                FinishedZmm = source.FinishedZmm,
                DensityKgM3 = source.DensityKgM3,
                HasCylindricalFace = source.HasCylindricalFace,
                LargestCylinderDiameterMm = source.LargestCylinderDiameterMm,
                ManufacturingTechnology = source.ManufacturingTechnology,
                Supplier = source.Supplier,
                Description = source.Description,
                SourcePath = source.SourcePath,
                RepresentativeComponentName = source.RepresentativeComponentName,
                Configuration = source.Configuration,
                IsLoaded = source.IsLoaded,
                IsVirtual = source.IsVirtual
            };
        }

        private static bool HasAny(IDictionary<string, string> properties, params string[] names)
        {
            foreach (string name in names)
                if (TryGet(properties, name, out string value) && !string.IsNullOrWhiteSpace(value)) return true;
            return false;
        }

        private static string Text(IDictionary<string, string> properties, params string[] names)
        {
            foreach (string name in names)
                if (TryGet(properties, name, out string value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
            return string.Empty;
        }

        private static int Integer(IDictionary<string, string> properties, params string[] names)
        {
            double value = Number(properties, names);
            return value > 0 ? Math.Max(1, (int)Math.Round(value)) : 0;
        }

        private static double Number(IDictionary<string, string> properties, params string[] names)
        {
            string text = Text(properties, names);
            if (string.IsNullOrWhiteSpace(text)) return 0;

            Match match = Regex.Match(text, @"[-+]?\d+(?:[\.,]\d+)?");
            if (!match.Success) return 0;
            string numeric = match.Value;
            if (numeric.Contains(",") && !numeric.Contains(".")) numeric = numeric.Replace(',', '.');
            return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? Math.Abs(value)
                : 0;
        }

        private static bool TryGet(IDictionary<string, string> properties, string name, out string value)
        {
            if (properties.TryGetValue(name, out value)) return true;
            foreach (KeyValuePair<string, string> pair in properties)
            {
                if (string.Equals(pair.Key?.Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }
    }
}

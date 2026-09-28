using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwMateAI.Core.Models.Understanding;

namespace SwMateAI.Core.Readers
{
    public class SolidWorksModelReader
    {
        private readonly ISldWorks _swApp;

        public SolidWorksModelReader(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        private IModelDoc2 ActiveModel()
        {
            return _swApp.ActiveDoc as IModelDoc2;
        }

        private IPartDoc ActivePart()
        {
            return ActiveModel() as IPartDoc;
        }

        public List<FeatureInfo> ReadFeatureTree()
        {
            var model = ActiveModel();
            var result = new List<FeatureInfo>();
            if (model == null) return result;

            var feature = model.FirstFeature() as IFeature;
            while (feature != null)
            {
                bool warning = false;
                int errorCode = feature.GetErrorCode2(out warning);
                bool isSketch = false;
                try { isSketch = feature.GetSpecificFeature2() is ISketch; } catch { }

                result.Add(new FeatureInfo
                {
                    Name = feature.Name ?? string.Empty,
                    TypeName = feature.GetTypeName2() ?? string.Empty,
                    ErrorCode = errorCode,
                    HasWarning = warning,
                    IsSketch = isSketch
                });
                feature = feature.GetNextFeature() as IFeature;
            }
            return result;
        }

        public List<FeatureInfo> ReadFeatures()
        {
            return ReadFeatureTree().Where(x => !x.IsSketch).ToList();
        }

        public List<FeatureInfo> ReadSketches()
        {
            return ReadFeatureTree().Where(x => x.IsSketch).ToList();
        }

        /// <summary>
        /// Reads direct parent/child relationships reported by SOLIDWORKS.
        /// No dependency is inferred: the graph only contains relationships
        /// returned by IFeature.GetParents/GetChildren.
        /// </summary>
        public List<FeatureDependencyInfo> ReadFeatureDependencies()
        {
            var model = ActiveModel();
            var result = new List<FeatureDependencyInfo>();
            if (model == null) return result;

            var feature = model.FirstFeature() as IFeature;
            while (feature != null)
            {
                bool isSketch = false;
                try { isSketch = feature.GetSpecificFeature2() is ISketch; } catch { }
                var item = new FeatureDependencyInfo
                {
                    Name = feature.Name ?? string.Empty,
                    TypeName = feature.GetTypeName2() ?? string.Empty,
                    IsSketch = isSketch
                };
                try { AddFeatureNames(item.Parents, feature.GetParents()); } catch { }
                try { AddFeatureNames(item.Children, feature.GetChildren()); } catch { }
                result.Add(item);
                feature = feature.GetNextFeature() as IFeature;
            }
            return result;
        }

        private static void AddFeatureNames(List<string> target, object raw)
        {
            if (target == null || raw == null) return;
            if (raw is Array array)
            {
                foreach (var value in array) AddFeatureName(target, value as IFeature);
                return;
            }
            AddFeatureName(target, raw as IFeature);
        }

        private static void AddFeatureName(List<string> target, IFeature feature)
        {
            string name = feature?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!target.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                target.Add(name);
        }

        public List<DimensionInfo> ReadDimensions()
        {
            var model = ActiveModel();
            var result = new List<DimensionInfo>();
            if (model == null) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var feature = model.FirstFeature() as IFeature;
            while (feature != null)
            {
                var display = feature.GetFirstDisplayDimension() as IDisplayDimension;
                while (display != null)
                {
                    var dimension = display.GetDimension2(0);
                    if (dimension != null && seen.Add(dimension.FullName))
                        result.Add(ToDimensionInfo(dimension, feature.Name));
                    display = feature.GetNextDisplayDimension(display) as IDisplayDimension;
                }
                feature = feature.GetNextFeature() as IFeature;
            }
            return result;
        }

        private static DimensionInfo ToDimensionInfo(IDimension dimension, string owner)
        {
            return new DimensionInfo
            {
                Name = dimension.Name ?? string.Empty,
                FullName = dimension.FullName ?? string.Empty,
                OwnerFeature = owner ?? string.Empty,
                ValueMm = dimension.SystemValue * 1000.0
            };
        }

        public string ReadMaterial()
        {
            var model = ActiveModel();
            var part = ActivePart();
            if (model == null || part == null) return string.Empty;

            string database;
            string config = model.ConfigurationManager.ActiveConfiguration?.Name ?? string.Empty;
            return part.GetMaterialPropertyName2(config, out database) ?? string.Empty;
        }

        public MassPropertiesInfo ReadMassProperties()
        {
            var model = ActiveModel();
            if (model == null) return null;
            var mass = model.Extension.CreateMassProperty() as IMassProperty;
            if (mass == null) return null;
            return new MassPropertiesInfo
            {
                MassKg = mass.Mass,
                VolumeMm3 = mass.Volume * 1e9,
                SurfaceAreaMm2 = mass.SurfaceArea * 1e6,
                DensityKgM3 = mass.Density
            };
        }

        public BoundingBoxInfo ReadBoundingBox()
        {
            var part = ActivePart();
            if (part == null) return null;
            var raw = part.GetPartBox(true) as Array;
            if (raw == null || raw.Length < 6) return null;

            double minX = Convert.ToDouble(raw.GetValue(0));
            double minY = Convert.ToDouble(raw.GetValue(1));
            double minZ = Convert.ToDouble(raw.GetValue(2));
            double maxX = Convert.ToDouble(raw.GetValue(3));
            double maxY = Convert.ToDouble(raw.GetValue(4));
            double maxZ = Convert.ToDouble(raw.GetValue(5));
            return new BoundingBoxInfo
            {
                Xmm = Math.Abs(maxX - minX) * 1000.0,
                Ymm = Math.Abs(maxY - minY) * 1000.0,
                Zmm = Math.Abs(maxZ - minZ) * 1000.0
            };
        }

        public List<CustomPropertyInfo> ReadCustomProperties()
        {
            var model = ActiveModel();
            var result = new List<CustomPropertyInfo>();
            if (model == null) return result;
            var manager = model.Extension.get_CustomPropertyManager(string.Empty);
            var names = manager?.GetNames() as Array;
            if (names == null) return result;

            foreach (var item in names)
            {
                string name = Convert.ToString(item) ?? string.Empty;
                string rawValue, resolvedValue;
                bool wasResolved, linkToProperty;
                manager.Get6(name, false, out rawValue, out resolvedValue, out wasResolved, out linkToProperty);
                result.Add(new CustomPropertyInfo
                {
                    Name = name,
                    RawValue = rawValue ?? string.Empty,
                    ResolvedValue = resolvedValue ?? string.Empty
                });
            }
            return result;
        }

        public List<SelectedObjectInfo> ReadSelectedObjects()
        {
            var model = ActiveModel();
            var result = new List<SelectedObjectInfo>();
            var selection = model?.SelectionManager as ISelectionMgr;
            if (selection == null) return result;

            int count = selection.GetSelectedObjectCount2(-1);
            for (int i = 1; i <= count; i++)
            {
                int typeId = selection.GetSelectedObjectType3(i, -1);
                object selected = selection.GetSelectedObject6(i, -1);
                result.Add(new SelectedObjectInfo
                {
                    Index = i,
                    TypeId = typeId,
                    TypeName = Enum.GetName(typeof(swSelectType_e), typeId) ?? typeId.ToString(),
                    Name = SelectionName(selected)
                });
            }
            return result;
        }

        private static string SelectionName(object selected)
        {
            if (selected is IFeature feature) return feature.Name ?? string.Empty;
            if (selected is IDisplayDimension display) return display.GetNameForSelection() ?? string.Empty;
            if (selected is IDimension dimension) return dimension.FullName ?? string.Empty;
            return selected?.GetType().Name ?? string.Empty;
        }
    }
}

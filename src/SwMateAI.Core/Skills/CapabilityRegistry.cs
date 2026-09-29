using System;
using System.Collections.Generic;

namespace SwMateAI.Core.Skills
{
    /// <summary>
    /// Evidence-backed capability status for the planner and UI.
    /// Only skills exercised by the live SOLIDWORKS integration runner are marked Verified.
    /// </summary>
    public static class CapabilityRegistry
    {
        private const string VerifiedVersion = "SOLIDWORKS 2021";
        private const string LiveEvidence =
            "SwMateAI.SolidWorks.IntegrationRunner live regression";

        private static readonly HashSet<string> LiveVerified =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                SkillNames.ObserveModel,
                SkillNames.CreateNewPart,
                SkillNames.CreateSketch,
                SkillNames.CreateRectangle,
                SkillNames.CreateExtrude,
                SkillNames.ReadFeatureTree,
                SkillNames.ReadFeatures,
                SkillNames.ReadFeatureDependencies,
                SkillNames.AnalyzeFeatureImpact,
                SkillNames.ReadSketches,
                SkillNames.ReadDimensions,                SkillNames.ReadMaterial,
                SkillNames.ReadMassProperties,
                SkillNames.ReadCustomProperties,
                SkillNames.ReadSelectedObject,
                SkillNames.ReadBoundingBox,
                SkillNames.ReadAssembly,
                SkillNames.ReadComponents,
                SkillNames.ReadMates,
                SkillNames.CheckInterference,
                SkillNames.InsertComponent,
                SkillNames.BuildManufacturingBreakdown,
                SkillNames.ExportManufacturingBreakdown,
                SkillNames.ApplyStockMaterials,
                SkillNames.CreateBOM,
                SkillNames.CreateDrawing,
                SkillNames.InsertStandardViews,
                SkillNames.InsertIsometricView,
                SkillNames.CreateSection,
                SkillNames.CreateDetail,
                SkillNames.InsertDimensions,
                SkillNames.InsertDrawingBOM,
                SkillNames.InsertBalloon,
                SkillNames.FillTitleBlock,
                SkillNames.ExportPDF,
                SkillNames.ExportDXF,
                SkillNames.ReadDrawing
            };

        public static void Apply(SkillMetadata metadata)        {
            if (metadata == null) return;

            bool verified = LiveVerified.Contains(metadata.Name);
            metadata.HasLiveRegression = verified;
            metadata.Maturity = verified ? SkillMaturity.Verified : SkillMaturity.Pilot;
            metadata.VerifiedSolidWorksVersion = verified ? VerifiedVersion : string.Empty;
            metadata.VerificationEvidence = verified
                ? LiveEvidence
                : "No dedicated live SOLIDWORKS regression recorded yet.";
        }

        public static bool IsLiveVerified(string skillName)
        {
            return !string.IsNullOrWhiteSpace(skillName) && LiveVerified.Contains(skillName);
        }

        public static IReadOnlyCollection<string> LiveVerifiedSkills => LiveVerified;
    }
}

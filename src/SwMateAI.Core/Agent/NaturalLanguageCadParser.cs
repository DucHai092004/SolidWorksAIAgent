using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SwMateAI.Core.Skills;

namespace SwMateAI.Core.Agent
{
    public class CadHoleSpec
    {
        public double Diameter { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class NaturalLanguageCadCommand
    {
        public string Intent { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double HoleDiameter { get; set; }
        public double HoleDepth { get; set; }
        public double HoleX { get; set; }
        public double HoleY { get; set; }
        public List<CadHoleSpec> Holes { get; } = new List<CadHoleSpec>();
        public double FilletRadius { get; set; }
        public double ChamferDistance { get; set; }
        public string DimensionName { get; set; } = string.Empty;
        public double DimensionValue { get; set; }
        public string TargetFeatureName { get; set; } = string.Empty;
        public string ComponentPath { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public string MateType { get; set; } = string.Empty;
        public string MateName { get; set; } = string.Empty;
        public double MateDistance { get; set; }
        public string ReplacementPath { get; set; } = string.Empty;
        public double StockAllowanceMm { get; set; } = 3.0;
        public string ExportPath { get; set; } = string.Empty;
        public bool BomExportExcel { get; set; }
        public bool BomExportCsv { get; set; }
        public string BomOutputFolder { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public string SheetPaperSize { get; set; } = "A3";
        public double SheetScaleNumerator { get; set; } = 1.0;
        public double SheetScaleDenominator { get; set; } = 1.0;
        public string DrawingProjection { get; set; } = "Third";
        public string SectionLabel { get; set; } = "A";
        public string SectionDirection { get; set; } = "Vertical";
        public string SectionSourceViewName { get; set; } = string.Empty;
    }

    public static class NaturalLanguageCadParser
    {
        public static bool TryParse(string input, out NaturalLanguageCadCommand command, out string error)
        {
            command = null; error = null;
            if (string.IsNullOrWhiteSpace(input)) { error = "Command is empty."; return false; }

            if (TryModifyDimension(input, out command))
                return true;

            if (TryFeatureImpactQuery(input, out command))
                return true;

            if (TrySectionQuery(input, out command))
                return true;

            if (TryIsometricViewQuery(input, out command))
                return true;

            if (TryStandardViewsQuery(input, out command))
                return true;

            if (TrySheetQuery(input, out command))
                return true;

            if (TryDrawingQuery(input, out command))
                return true;

            if (TryNativeBomQuery(input, out command))
                return true;

            if (TryBomQuery(input, out command))
                return true;

            if (TryManufacturingBreakdownQuery(input, out command))
                return true;

            if (TryAssemblyActionQuery(input, out command))
                return true;

            if (TryAssemblyReadQuery(input, out command))
                return true;

            if (TryReadModelQuery(input, out command))
                return true;

            string s = input.Trim().ToLowerInvariant()
                .Replace("×", "x").Replace("φ", "phi").Replace("ø", "phi");

            if (!TryPlateDimensions(s, out double w, out double h, out double t))
            {
                error = "Could not find plate dimensions. Try '120 x 80 x 15 mm' or '120 x 80 mm, dày 15 mm'.";
                return false;
            }
            if (w <= 0 || h <= 0 || t <= 0) { error = "Plate dimensions must be greater than zero."; return false; }

            var parsed = new NaturalLanguageCadCommand { Width = w, Height = h, Thickness = t, HoleDepth = t };

            var holeMatches = Regex.Matches(s, @"(?:lỗ\s*|lo\s*|hole\s*)?(?:phi|diameter|dia|đường\s*kính|duong\s*kinh)\s*[:=]?\s*(?<d>\d+(?:[\.,]\d+)?)");
            for (int i = 0; i < holeMatches.Count; i++)
            {
                var match = holeMatches[i];
                double d = Number(match.Groups["d"].Value);
                int segmentStart = match.Index + match.Length;
                int segmentEnd = i + 1 < holeMatches.Count ? holeMatches[i + 1].Index : s.Length;
                string segment = s.Substring(segmentStart, segmentEnd - segmentStart);
                double x = Coordinate(segment, "x");
                double y = Coordinate(segment, "y");

                if (d <= 0 || d >= Math.Min(w, h)) { error = "Hole diameter is invalid for this plate."; return false; }
                double margin = d / 2.0;
                if (Math.Abs(x) + margin > w / 2.0 || Math.Abs(y) + margin > h / 2.0)
                {
                    error = $"Hole Ø{d:0.###} at X={x:0.###}, Y={y:0.###} would be outside the plate.";
                    return false;
                }
                parsed.Holes.Add(new CadHoleSpec { Diameter = d, X = x, Y = y });
            }

            var fillet = Regex.Match(s, @"(?:bo\s*(?:tròn\s*)?(?:4\s*)?(?:góc|goc)|fillet)\s*(?:r\s*[:=]?\s*)?(?<r>\d+(?:[\.,]\d+)?)");
            var chamfer = Regex.Match(s, @"(?:vát|vat|chamfer)\s*(?:4\s*)?(?:góc|goc|mép|mep)?\s*(?<c>\d+(?:[\.,]\d+)?)");
            if (fillet.Success) parsed.FilletRadius = Number(fillet.Groups["r"].Value);
            if (chamfer.Success) parsed.ChamferDistance = Number(chamfer.Groups["c"].Value);

            if (parsed.FilletRadius > 0 && parsed.ChamferDistance > 0)
            {
                error = "Use either fillet or chamfer in one command, not both.";
                return false;
            }
            if (parsed.FilletRadius >= Math.Min(w, h) / 2.0 || parsed.ChamferDistance >= Math.Min(w, h) / 2.0)
            {
                error = "Corner treatment is too large for this plate.";
                return false;
            }

            parsed.Intent = parsed.Holes.Count > 0 ? "CreatePlateWithHole" : "CreatePlate";
            if (parsed.Holes.Count > 0)
            {
                parsed.HoleDiameter = parsed.Holes[0].Diameter;
                parsed.HoleX = parsed.Holes[0].X;
                parsed.HoleY = parsed.Holes[0].Y;
            }

            command = parsed;
            return true;
        }


        private static bool TryFeatureImpactQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string token = @"(?<name>""[^""]+""|[A-Za-z][A-Za-z0-9_\-]*\d[A-Za-z0-9_\-]*)";
            string[] patterns =
            {
                @"(?:nếu\s*(?:sửa|thay\s*đổi)|neu\s*(?:sua|thay\s*doi)|if\s+(?:i\s+)?(?:change|modify))\s*" + token,
                token + @"\s*(?:ảnh\s*hưởng|anh\s*huong|affect)",
                @"(?:feature\s*nào\s*phụ\s*thuộc|feature\s*nao\s*phu\s*thuoc|what\s+depends\s+on|impact\s+of)\s*" + token
            };
            foreach (var pattern in patterns)
            {
                var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
                if (!match.Success) continue;
                string name = match.Groups["name"].Value.Trim().Trim('\"');
                command = new NaturalLanguageCadCommand { Intent = SkillNames.AnalyzeFeatureImpact, TargetFeatureName = name };
                return true;
            }
            return false;
        }

        private static bool TrySectionQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            bool section = Regex.IsMatch(s, @"(?:mặt\s*cắt|mat\s*cat|section(?:\s*view)?)");
            bool action = Regex.IsMatch(s, @"(?:tạo|tao|chèn|chen|thêm|them|create|insert|add)");
            if (!section || !action) return false;

            string label = "A";
            var labelMatch = Regex.Match(input,
                @"(?:mặt\s*cắt|mat\s*cat|section(?:\s*view)?)\s*(?<label>[A-Za-z])(?:\s*-\s*\k<label>)?",
                RegexOptions.IgnoreCase);
            if (labelMatch.Success) label = labelMatch.Groups["label"].Value.ToUpperInvariant();

            string direction = "Vertical";
            if (Regex.IsMatch(s, @"(?:ngang|horizontal)")) direction = "Horizontal";
            else if (Regex.IsMatch(s, @"(?:dọc|doc|đứng|dung|vertical)")) direction = "Vertical";

            string sourceView = string.Empty;
            var viewMatch = Regex.Match(input,
                @"(?:từ|tu|from)\s*(?:""(?<view>[^""]+)""|(?<view>Drawing\s+View\d+))",
                RegexOptions.IgnoreCase);
            if (viewMatch.Success) sourceView = viewMatch.Groups["view"].Value.Trim();

            command = new NaturalLanguageCadCommand
            {
                Intent = SkillNames.CreateSection,
                SectionLabel = label,
                SectionDirection = direction,
                SectionSourceViewName = sourceView
            };
            return true;
        }

        private static bool TryIsometricViewQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            bool iso = Regex.IsMatch(s,
                @"(?:isometric|iso\s*view|hình\s*chiếu\s*trục\s*đo|hinh\s*chieu\s*truc\s*do|hình\s*chiếu\s*iso|hinh\s*chieu\s*iso)");
            bool action = Regex.IsMatch(s, @"(?:chèn|chen|thêm|them|tạo|tao|insert|add|create)");
            if (!iso || !action) return false;

            command = new NaturalLanguageCadCommand
            {
                Intent = SkillNames.InsertIsometricView
            };
            return true;
        }

        private static bool TryStandardViewsQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            bool standard = Regex.IsMatch(s,
                @"(?:hình\s*chiếu\s*chuẩn|hinh\s*chieu\s*chuan|3\s*hình\s*chiếu|3\s*hinh\s*chieu|standard\s*views?|orthographic\s*views?|front\s*[,/+&-]?\s*top\s*[,/+&-]?\s*(?:right|side))");
            bool action = Regex.IsMatch(s, @"(?:chèn|chen|thêm|them|tạo|tao|insert|add|create)");
            if (!standard || !action) return false;

            string projection = "Third";
            if (Regex.IsMatch(s, @"(?:góc\s*thứ\s*nhất|goc\s*thu\s*nhat|first[-\s]*angle|1st[-\s]*angle)"))
                projection = "First";
            else if (Regex.IsMatch(s, @"(?:góc\s*thứ\s*ba|goc\s*thu\s*ba|third[-\s]*angle|3rd[-\s]*angle)"))
                projection = "Third";

            command = new NaturalLanguageCadCommand
            {
                Intent = SkillNames.InsertStandardViews,
                DrawingProjection = projection
            };
            return true;
        }

        private static bool TrySheetQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            bool create = Regex.IsMatch(s, @"(?:thêm|them|tạo|tao|add|create|new)\s*(?:(?:một|mot|a|an)\s*)?(?:a[0-4]\s*)?sheet\b|\bsheet\s*(?:mới|moi|new)\b");
            if (!create) return false;

            string paper = "A3";
            var paperMatch = Regex.Match(input, @"\b(?<paper>A[0-4])\b", RegexOptions.IgnoreCase);
            if (paperMatch.Success) paper = paperMatch.Groups["paper"].Value.ToUpperInvariant();

            string name = string.Empty;
            var nameMatch = Regex.Match(input, @"(?:tên|ten|name)\s*[:=]?\s*(?:""(?<name>[^""]+)""|(?<name>[A-Za-z0-9_.\-]+))", RegexOptions.IgnoreCase);
            if (nameMatch.Success) name = nameMatch.Groups["name"].Value.Trim();

            double scaleNum = 1.0;
            double scaleDen = 1.0;
            var scaleMatch = Regex.Match(input, @"(?:tỷ\s*lệ|ty\s*le|scale)\s*[:=]?\s*(?<n>\d+(?:[\.,]\d+)?)\s*[:/]\s*(?<d>\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
            if (scaleMatch.Success)
            {
                scaleNum = Number(scaleMatch.Groups["n"].Value);
                scaleDen = Number(scaleMatch.Groups["d"].Value);
                if (scaleNum <= 0 || scaleDen <= 0) return false;
            }

            command = new NaturalLanguageCadCommand
            {
                Intent = SkillNames.CreateSheet,
                SheetName = name,
                SheetPaperSize = paper,
                SheetScaleNumerator = scaleNum,
                SheetScaleDenominator = scaleDen
            };
            return true;
        }

        private static bool TryDrawingQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            bool create = Regex.IsMatch(s, @"(?:tạo|tao|create|new|lập|lap)\s*(?:một\s*)?(?:drawing|bản\s*vẽ|ban\s*ve)");
            bool explicitTarget = Regex.IsMatch(s, @"(?:cho|from|for)\s*(?:part|assembly|chi\s*tiết|chi\s*tiet|cụm|cum|model|này|nay|this)");
            bool simpleCreate = Regex.IsMatch(s, @"^(?:tạo|tao|create)\s*(?:drawing|bản\s*vẽ|ban\s*ve)\s*$");
            if (!create || (!explicitTarget && !simpleCreate)) return false;
            command = new NaturalLanguageCadCommand { Intent = SkillNames.CreateDrawing };
            return true;
        }

        private static bool TryNativeBomQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            bool bom = Regex.IsMatch(s, @"\bbom\b|bill\s*of\s*materials");
            bool native = Regex.IsMatch(s, @"solidworks|native|trong\s*assembly|vào\s*assembly|vao\s*assembly|chèn|chen|insert");
            if (!bom || !native) return false;
            command = new NaturalLanguageCadCommand { Intent = SkillNames.InsertSolidWorksBOM };
            return true;
        }

        private static bool TryBomQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(s, @"\bbom\b|bill\s*of\s*materials|danh\s*sách\s*vật\s*tư|danh\s*sach\s*vat\s*tu")) return false;
            bool excel = Regex.IsMatch(s, @"excel|xlsx");
            bool csv = Regex.IsMatch(s, @"\bcsv\b");
            command = new NaturalLanguageCadCommand { Intent = SkillNames.CreateBOM, BomExportExcel = excel, BomExportCsv = csv };
            return true;
        }

        private static bool TryManufacturingBreakdownQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(s, @"(?:bóc\s*tách|boc\s*tach|manufacturing\s*breakdown|breakdown\s*(?:parts|assembly)?|chi\s*tiết\s*gia\s*công|chi\s*tiet\s*gia\s*cong)")) return false;
            double allowance = 3.0;
            var m = Regex.Match(input, @"(?:lượng\s*dư|luong\s*du|allowance)\s*[:=]?\s*(?<v>\d+(?:[\.,]\d+)?)\s*(?:mm)?", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(input, @"(?<v>\d+(?:[\.,]\d+)?)\s*(?:mm)?\s*allowance", RegexOptions.IgnoreCase);
            if (m.Success) allowance = Number(m.Groups["v"].Value);
            bool wantsExport = Regex.IsMatch(s, @"(?:xuất|xuat|export)\s*(?:excel|xlsx)?|(?:excel|xlsx)");
            string exportPath = string.Empty;
            var pathMatch = Regex.Match(input, @"(?:""(?<p>[A-Za-z]:\\[^""]+\.xlsx)""|(?<p>[A-Za-z]:\\[^\r\n]+?\.xlsx))", RegexOptions.IgnoreCase);
            if (pathMatch.Success) exportPath = pathMatch.Groups["p"].Value;
            command = new NaturalLanguageCadCommand { Intent = wantsExport ? SkillNames.ExportManufacturingBreakdown : SkillNames.BuildManufacturingBreakdown, StockAllowanceMm = allowance, ExportPath = exportPath };
            return true;
        }

        private static bool TryAssemblyActionQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            var deleteMate = Regex.Match(input,
                @"(?:xóa|xoá|xoa|delete|remove)\s+(?:(?:mate|ràng\s*buộc|rang\s*buoc)\s+)?(?:""(?<name>[^""]+)""|(?<name>[A-Za-z0-9_.\-]+))",
                RegexOptions.IgnoreCase);
            if (deleteMate.Success)
            {
                command = new NaturalLanguageCadCommand { Intent = SkillNames.DeleteMate, MateName = deleteMate.Groups["name"].Value };
                return true;
            }

            var replace = Regex.Match(input,
                @"(?:thay|replace)\s+(?:component\s+)?(?:""(?<name>[^""]+)""|(?<name>[A-Za-z0-9_.\-]+))\s+(?:bằng|bang|with)\s*(?:""(?<path>[A-Za-z]:\\[^""]+\.(?:sldprt|sldasm))""|(?<path>[A-Za-z]:\\.+?\.(?:sldprt|sldasm)))",
                RegexOptions.IgnoreCase);
            if (replace.Success)
            {
                command = new NaturalLanguageCadCommand {
                    Intent = SkillNames.ReplaceComponent,
                    ComponentName = replace.Groups["name"].Value,
                    ReplacementPath = replace.Groups["path"].Value };
                return true;
            }

            string mateTypePattern = @"coincident|concentric|parallel|perpendicular|tangent|distance|đồng\s*tâm|dong\s*tam|song\s*song|vuông\s*góc|vuong\s*goc|tiếp\s*tuyến|tiep\s*tuyen|khoảng\s*cách|khoang\s*cach";
            var addMate = Regex.Match(input,
                @"(?:thêm|them|add)\s+(?:(?:mate|ràng\s*buộc|rang\s*buoc)\s*(?<type1>" + mateTypePattern + @")?|(?<type2>" + mateTypePattern + @")\s+(?:mate|ràng\s*buộc|rang\s*buoc))",
                RegexOptions.IgnoreCase);
            if (addMate.Success)
            {
                string mateType = !string.IsNullOrWhiteSpace(addMate.Groups["type1"].Value) ? addMate.Groups["type1"].Value : addMate.Groups["type2"].Value;
                if (string.IsNullOrWhiteSpace(mateType)) mateType = "coincident";
                double mateDistance = 0;
                var distance = Regex.Match(input, @"(?:distance|khoảng\s*cách|khoang\s*cach)\s*[:=]?\s*(?<d>\d+(?:[\.,]\d+)?)\s*(?:mm)?", RegexOptions.IgnoreCase);
                if (distance.Success) mateDistance = Number(distance.Groups["d"].Value);
                command = new NaturalLanguageCadCommand { Intent = SkillNames.AddMate, MateType = mateType, MateDistance = mateDistance };
                return true;
            }

            var insert = Regex.Match(input,
                @"(?:chèn|chen|thêm|them|insert|add)\s+(?:component|chi\s*tiết|chi\s*tiet)?\s*(?:""(?<path>[A-Za-z]:\\[^""]+\.(?:sldprt|sldasm))""|(?<path>[A-Za-z]:\\.+?\.(?:sldprt|sldasm)))",
                RegexOptions.IgnoreCase);
            if (insert.Success)
            {
                string lower = input.ToLowerInvariant();
                command = new NaturalLanguageCadCommand {
                    Intent = SkillNames.InsertComponent, ComponentPath = insert.Groups["path"].Value,
                    PositionX = Coordinate(lower, "x"), PositionY = Coordinate(lower, "y"), PositionZ = Coordinate(lower, "z") };
                return true;
            }

            var move = Regex.Match(input,
                @"(?:di\s*chuyển|di\s*chuyen|move)\s+(?:component\s+)?(?:""(?<name>[^""]+)""|(?<name>[A-Za-z0-9_.\-]+))",
                RegexOptions.IgnoreCase);
            if (move.Success)
            {
                string lower = input.ToLowerInvariant();
                command = new NaturalLanguageCadCommand {
                    Intent = SkillNames.MoveComponent, ComponentName = move.Groups["name"].Value,
                    PositionX = Coordinate(lower, "x"), PositionY = Coordinate(lower, "y"), PositionZ = Coordinate(lower, "z") };
                return true;
            }
            return false;
        }

        private static bool TryAssemblyReadQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            string intent = null;
            if (Regex.IsMatch(s, @"(?:interference|collision|va\s*chạm|va\s*cham|giao\s*nhau|xuyên\s*nhau|xuyen\s*nhau)")) intent = SkillNames.CheckInterference;
            else if (Regex.IsMatch(s, @"(?:mate|ràng\s*buộc|rang\s*buoc)") && Regex.IsMatch(s, @"(?:assembly|cụm|cum|mate)")) intent = SkillNames.ReadMates;
            else if (Regex.IsMatch(s, @"(?:component|linh\s*kiện|linh\s*kien|chi\s*tiết|chi\s*tiet)") && Regex.IsMatch(s, @"(?:assembly|cụm|cum|component)")) intent = SkillNames.ReadComponents;
            else if (Regex.IsMatch(s, @"(?:đọc|doc|thông\s*tin|thong\s*tin|tổng\s*quan|tong\s*quan|summary|read|assembly|cụm|cum)\s*.*(?:assembly|cụm|cum)|(?:assembly|cụm|cum)\s*(?:này|nay)?\s*(?:có\s*gì|co\s*gi|summary|information)?")) intent = SkillNames.ReadAssembly;
            if (intent == null) return false;
            command = new NaturalLanguageCadCommand { Intent = intent };
            return true;
        }

        private static bool TryReadModelQuery(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            string s = input.Trim().ToLowerInvariant();
            string intent = null;

            if (Regex.IsMatch(s, @"(?:feature\s*(?:dependency|dependencies|relation|relationship)|design\s*intent|quan\s*hệ\s*feature|quan\s*he\s*feature|feature\s*nào\s*phụ\s*thuộc|feature\s*nao\s*phu\s*thuoc|phụ\s*thuộc\s*feature|phu\s*thuoc\s*feature|cây\s*phụ\s*thuộc|cay\s*phu\s*thuoc)") ) intent = SkillNames.ReadFeatureDependencies;
            else if (Regex.IsMatch(s, @"(?:bounding\s*box|kích\s*thước\s*tổng\s*thể|kich\s*thuoc\s*tong\s*the|overall\s*size|overall\s*dimensions)") ) intent = SkillNames.ReadBoundingBox;
            else if (Regex.IsMatch(s, @"(?:vật\s*liệu|vat\s*lieu|material)") ) intent = SkillNames.ReadMaterial;
            else if (Regex.IsMatch(s, @"(?:khối\s*lượng|khoi\s*luong|trọng\s*lượng|trong\s*luong|mass|weight|thể\s*tích|the\s*tich|volume)") ) intent = SkillNames.ReadMassProperties;
            else if (Regex.IsMatch(s, @"(?:custom\s*propert|thuộc\s*tính\s*tùy\s*chỉnh|thuoc\s*tinh\s*tuy\s*chinh)") ) intent = SkillNames.ReadCustomProperties;
            else if (Regex.IsMatch(s, @"(?:đang\s*chọn\s*gì|dang\s*chon\s*gi|selected\s*object|what.*selected)") ) intent = SkillNames.ReadSelectedObject;
            else if (Regex.IsMatch(s, @"(?:dimension|kích\s*thước\s*nào|kich\s*thuoc\s*nao|các\s*kích\s*thước|cac\s*kich\s*thuoc)") ) intent = SkillNames.ReadDimensions;
            else if (Regex.IsMatch(s, @"(?:sketch|phác\s*thảo|phac\s*thao)") ) intent = SkillNames.ReadSketches;
            else if (Regex.IsMatch(s, @"(?:feature\s*tree|cây\s*feature|cay\s*feature)") ) intent = SkillNames.ReadFeatureTree;
            else if (Regex.IsMatch(s, @"(?:feature|đặc\s*trưng|dac\s*trung)") ) intent = SkillNames.ReadFeatures;

            if (intent == null) return false;
            command = new NaturalLanguageCadCommand { Intent = intent };
            return true;
        }

        private static bool TryModifyDimension(string input, out NaturalLanguageCadCommand command)
        {
            command = null;
            var match = Regex.Match(input,
                @"(?:đổi|doi|sửa|sua|thay\s*đổi|thay\s*doi|change|modify|set)\s*(?:kích\s*thước|kich\s*thuoc|dimension)?\s*(?<name>D\d+@[A-Za-z0-9_\-]+)\s*(?:thành|thanh|to|=|:)\s*(?<value>\d+(?:[\.,]\d+)?)\s*(?:mm)?",
                RegexOptions.IgnoreCase);
            if (!match.Success) return false;

            double value = Number(match.Groups["value"].Value);
            if (value <= 0) return false;

            command = new NaturalLanguageCadCommand
            {
                Intent = "ModifyDimension",
                DimensionName = match.Groups["name"].Value,
                DimensionValue = value
            };
            return true;
        }

        private static bool TryPlateDimensions(string s, out double w, out double h, out double t)
        {
            w = h = t = 0;
            var triple = Regex.Match(s, @"(?<w>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<h>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<t>\d+(?:[\.,]\d+)?)");
            if (triple.Success)
            {
                w = Number(triple.Groups["w"].Value);
                h = Number(triple.Groups["h"].Value);
                t = Number(triple.Groups["t"].Value);
                return true;
            }

            var wh = Regex.Match(s, @"(?<w>\d+(?:[\.,]\d+)?)\s*(?:mm\s*)?x\s*(?<h>\d+(?:[\.,]\d+)?)");
            var thick = Regex.Match(s, @"(?:dày|day|thickness|thick\.?|t)\s*[:=]?\s*(?<t>\d+(?:[\.,]\d+)?)");
            if (!wh.Success || !thick.Success) return false;

            w = Number(wh.Groups["w"].Value);
            h = Number(wh.Groups["h"].Value);
            t = Number(thick.Groups["t"].Value);
            return true;
        }

        private static double Coordinate(string segment, string axis)
        {
            var m = Regex.Match(segment, $@"(?:tọa\s*độ\s*|toa\s*do\s*)?\b{axis}\s*[:=]\s*(?<v>-?\d+(?:[\.,]\d+)?)");
            return m.Success ? Number(m.Groups["v"].Value) : 0;
        }

        private static double Number(string value) =>
            double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}

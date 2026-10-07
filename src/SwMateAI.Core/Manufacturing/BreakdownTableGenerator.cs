using System;
using System.Collections.Generic;
using System.Linq;

namespace SwMateAI.Core.Manufacturing
{
    public class BreakdownTableGenerator
    {
        public BreakdownTable Generate(BreakdownResult result)
        {
            var table = new BreakdownTable();
            table.Headers.AddRange(new[]
            {
                "Hình ảnh", "Part Number", "Tên chi tiết", "Số lượng",
                "Vật liệu CAD", "Vật liệu phôi", "Kích thước thành phẩm",
                "Nhóm CAD / Cut-List", "Loại phôi", "Kích thước phôi",
                "Dài phôi phẳng", "Rộng phôi phẳng", "Dày tấm",
                "Dài cắt Weldment", "Góc cắt 1", "Góc cắt 2",
                "Khối lượng phôi", "Nguồn vật liệu phôi", "Vị trí nguồn",
                "Virtual Part", "Công nghệ gia công", "Nhà gia công",
                "Trạng thái", "Bằng chứng"
            });
            if (result == null) return table;

            foreach (var item in result.Items
                .OrderBy(x => x.StockMaterial ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.StockType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.StockSize ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.PartNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                table.Rows.Add(new List<object>
                {
                    item.ImagePath,
                    item.PartNumber,
                    item.PartName,
                    item.Quantity,
                    item.Material,
                    item.StockMaterial,
                    item.FinishedSize,
                    item.ManufacturingForm,
                    item.StockType,
                    item.StockSize,
                    NumberOrBlank(item.FlatBlankLengthMm),
                    NumberOrBlank(item.FlatBlankWidthMm),
                    NumberOrBlank(item.SheetMetalThicknessMm),
                    NumberOrBlank(item.WeldmentCutLengthMm),
                    NumberOrBlank(item.WeldmentAngle1Deg),
                    NumberOrBlank(item.WeldmentAngle2Deg),
                    NumberOrBlank(item.StockWeightKg),
                    item.StockMaterialSource,
                    item.StockMaterialSourceLocation,
                    item.IsVirtual ? "Có" : "Không",
                    item.ManufacturingTechnology,
                    item.Supplier,
                    Status(item),
                    item.ManufacturingEvidence
                });
            }
            return table;
        }

        private static object NumberOrBlank(double value)
            => value > 0 ? (object)value : string.Empty;

        private static string Status(BreakdownItem item)
        {
            if (item == null) return string.Empty;
            if (!item.IsLoaded) return "Chưa load Part";
            if (item.StockMaterialNeedsReview) return "Cần kiểm tra vật liệu phôi";
            if (string.IsNullOrWhiteSpace(item.Material)) return "Thiếu vật liệu CAD";
            return "OK";
        }
    }
}

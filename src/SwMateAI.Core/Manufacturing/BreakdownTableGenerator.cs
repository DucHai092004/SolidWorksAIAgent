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
                "Loại phôi", "Kích thước phôi", "Khối lượng phôi",
                "Nguồn vật liệu phôi", "Vị trí nguồn", "Trạng thái"
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
                    item.StockType,
                    item.StockSize,
                    item.StockWeightKg > 0 ? (object)item.StockWeightKg : string.Empty,
                    item.StockMaterialSource,
                    item.StockMaterialSourceLocation,
                    item.StockMaterialNeedsReview ? "Cần kiểm tra" : "OK"
                });
            }
            return table;
        }
    }
}

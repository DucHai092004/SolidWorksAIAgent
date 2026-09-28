namespace SwMateAI.Core.Manufacturing
{
    public class BreakdownTableGenerator
    {
        public BreakdownTable Generate(BreakdownResult result)
        {
            var table = new BreakdownTable();
            table.Headers.AddRange(new[]
            {
                "Hình ảnh", "Part Number", "Tên chi tiết", "Số lượng", "Vật liệu",
                "Kích thước thành phẩm", "Loại phôi", "Kích thước phôi", "Khối lượng phôi",
                "Công nghệ gia công", "Nhà gia công"
            });
            if (result == null) return table;

            foreach (var item in result.Items)
            {
                table.Rows.Add(new System.Collections.Generic.List<object>
                {
                    item.ImagePath,
                    item.PartNumber,
                    item.PartName,
                    item.Quantity,
                    item.Material,
                    item.FinishedSize,
                    item.StockType,
                    item.StockSize,
                    item.StockWeightKg > 0 ? (object)item.StockWeightKg : string.Empty,
                    string.Empty,
                    string.Empty
                });
            }
            return table;
        }
    }
}

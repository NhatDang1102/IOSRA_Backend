using System.IO;
using ClosedXML.Excel;
using Contract.DTOs.Response.OperationMod;

namespace Service.Helpers
{
    public static class ExcelHelper
    {
        public static byte[] GenerateRevenueExcel(OperationRevenueResponse data)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Revenue Stats");

            // Headers
            worksheet.Cell(1, 1).Value = "Period";
            worksheet.Cell(1, 2).Value = "Value (VND)";
            worksheet.Range(1, 1, 1, 2).Style.Font.Bold = true;

            int row = 2;
            foreach (var point in data.Points)
            {
                worksheet.Cell(row, 1).Value = point.PeriodLabel;
                worksheet.Cell(row, 2).Value = point.Value;
                row++;
            }

            // Summary
            row++;
            worksheet.Cell(row, 1).Value = "Total Dia Topup";
            worksheet.Cell(row, 2).Value = data.DiaTopup;
            row++;
            worksheet.Cell(row, 1).Value = "Total Subscription";
            worksheet.Cell(row, 2).Value = data.Subscription;
            row++;
            worksheet.Cell(row, 1).Value = "Total Voice Topup";
            worksheet.Cell(row, 2).Value = data.VoiceTopup;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public static byte[] GenerateRequestStatsExcel(OperationRequestStatResponse data)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Request Stats");

            worksheet.Cell(1, 1).Value = "Request Type";
            worksheet.Cell(1, 2).Value = data.Type;
            worksheet.Range(1, 1, 1, 2).Style.Font.Bold = true;

            worksheet.Cell(3, 1).Value = "Period";
            worksheet.Cell(3, 2).Value = "Count";
            worksheet.Range(3, 1, 3, 2).Style.Font.Bold = true;

            int row = 4;
            foreach (var point in data.Points)
            {
                worksheet.Cell(row, 1).Value = point.PeriodLabel;
                worksheet.Cell(row, 2).Value = point.Value;
                row++;
            }

            row++;
            worksheet.Cell(row, 1).Value = "Total";
            worksheet.Cell(row, 2).Value = data.Total;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public static byte[] GenerateAuthorRevenueExcel(OperationAuthorRevenueResponse data)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Author Revenue");

            worksheet.Cell(1, 1).Value = "Metric";
            worksheet.Cell(1, 2).Value = data.Metric;
            worksheet.Range(1, 1, 1, 2).Style.Font.Bold = true;

            worksheet.Cell(3, 1).Value = "Period";
            worksheet.Cell(3, 2).Value = "Value";
            worksheet.Range(3, 1, 3, 2).Style.Font.Bold = true;

            int row = 4;
            foreach (var point in data.Points)
            {
                worksheet.Cell(row, 1).Value = point.PeriodLabel;
                worksheet.Cell(row, 2).Value = point.Value;
                row++;
            }

            row++;
            worksheet.Cell(row, 1).Value = "Total";
            worksheet.Cell(row, 2).Value = data.Total;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}

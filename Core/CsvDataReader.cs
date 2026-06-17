using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace DbAutoInsert.Core;

public static class CsvDataReader
{
    /// <summary>헤더와 데이터 행을 함께 반환</summary>
    public static (List<Dictionary<string, string>> Rows, List<string> Headers) ReadWithHeaders(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();
        var headers = (csv.HeaderRecord ?? Array.Empty<string>())
                      .Select(h => h.Trim().ToUpperInvariant())  // 헤더 대문자 통일
                      .ToList();

        var rows = new List<Dictionary<string, string>>();
        while (csv.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in headers)
                row[h] = csv.GetField(h) ?? string.Empty;
            rows.Add(row);
        }
        return (rows, headers);
    }
}
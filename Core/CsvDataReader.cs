using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DbAutoInsert.Core
{
    public static class CsvDataReader
    {
        public static Tuple<List<Dictionary<string, string>>, List<string>> ReadWithHeaders(string filePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord   = true,
                TrimOptions       = TrimOptions.Trim,
                MissingFieldFound = null,
                BadDataFound      = null
            };

            var raw  = File.ReadAllBytes(filePath);
            var text = Encoding.UTF8.GetString(raw)
                           .Replace("\r\n", "\n")
                           .Replace("\r", "\n");

            using (var sr = new StringReader(text))
            using (var csv = new CsvHelper.CsvReader(sr, config))
            {
                csv.Read();
                csv.ReadHeader();

                // 원본 헤더 (소문자 그대로) 보존
                var originalHeaders = (csv.HeaderRecord ?? Array.Empty<string>())
                                      .Select(h => h.Trim())
                                      .ToList();

                // 대문자 헤더 (Oracle 컬럼명용)
                var upperHeaders = originalHeaders
                                   .Select(h => h.ToUpperInvariant())
                                   .ToList();

                var rows = new List<Dictionary<string, string>>();
                while (csv.Read())
                {
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < originalHeaders.Count; i++)
                    {
                        // 원본 헤더명으로 값 가져와서 대문자 키로 저장
                        var val = (csv.GetField(originalHeaders[i]) ?? string.Empty).Trim();
                        row[upperHeaders[i]] = val;
                    }
                    rows.Add(row);
                }
                return Tuple.Create(rows, upperHeaders);
            }
        }
    }
}

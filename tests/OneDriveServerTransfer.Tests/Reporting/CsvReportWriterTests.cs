using OneDriveServerTransfer.Reporting;

namespace OneDriveServerTransfer.Tests.Reporting;

/// <summary>
/// Verifies the audit CSV writer (docs/REPORT_SCHEMA.md, "CSV safety"): the exact
/// schema-version-1 header order, RFC 4180 escaping, spreadsheet formula-injection
/// neutralization (leading apostrophe policy), and Unicode preservation.
/// </summary>
public class CsvReportWriterTests
{
    [Fact]
    public void HeaderMatchesSchemaVersion1Exactly()
    {
        Assert.Equal(
            "ReportSchemaVersion,RunId,OperatorUPN,EmployeeUPN,SourceDriveId,SourceItemId," +
            "SourcePath,LocalPath,ItemType,SizeBytes,Status,AttemptCount,SourceHashType," +
            "SourceHashValue,LocalSha256,TimestampStatus,ErrorCode,ErrorMessage," +
            "StartedAtUtc,CompletedAtUtc",
            CsvReportWriter.ItemsHeader);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("with,comma", "\"with,comma\"")]
    [InlineData("with\"quote", "\"with\"\"quote\"")]
    [InlineData("with\rcr", "\"with\rcr\"")]
    [InlineData("with\nlf", "\"with\nlf\"")]
    [InlineData("with\r\ncrlf", "\"with\r\ncrlf\"")]
    [InlineData("تقرير الملف.csv", "تقرير الملف.csv")]
    [InlineData("emoji 📁 name", "emoji 📁 name")]
    public void EscapeAppliesRfc4180Rules(string? input, string expected) =>
        Assert.Equal(expected, CsvReportWriter.Escape(input));

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+1+2", "'+1+2")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\t=1", "'\t=1")]
    [InlineData("\r=1", "\"'\r=1\"")]
    [InlineData("\n=1", "\"'\n=1\"")]
    [InlineData("=HYPERLINK(\"http://evil.test\",\"x\")", "\"'=HYPERLINK(\"\"http://evil.test\"\",\"\"x\"\")\"")]
    public void EscapeNeutralizesFormulaInjectionWithLeadingApostrophe(string input, string expected) =>
        Assert.Equal(expected, CsvReportWriter.Escape(input));

    [Fact]
    public void WriteRowJoinsEscapedFieldsWithCrlfTerminator()
    {
        var writer = new StringWriter();
        CsvReportWriter.WriteRow(writer, ["a", "b,c", null, "d"]);

        Assert.Equal("a,\"b,c\",,d\r\n", writer.ToString());
    }

    [Fact]
    public void TrickyValuesRoundTripThroughEscaping()
    {
        var values = new[]
        {
            "comma,value", "quo\"te", "line\nbreak", "carriage\rreturn",
            "=formula", "@mention", "تقرير", " leading space", "trailing ",
        };

        var writer = new StringWriter();
        CsvReportWriter.WriteRow(writer, values);

        var parsed = ParseRow(writer.ToString());
        Assert.Equal(values.Length, parsed.Count);
        for (var i = 0; i < values.Length; i++)
        {
            var expected = values[i][0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n'
                ? "'" + values[i]
                : values[i];
            Assert.Equal(expected, parsed[i]);
        }
    }

    /// <summary>Minimal single-row CSV parser for assertions.</summary>
    private static List<string> ParseRow(string row)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < row.Length; i++)
        {
            var c = row[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < row.Length && row[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else if (c == '\r' && i + 1 < row.Length && row[i + 1] == '\n')
            {
                break;
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}

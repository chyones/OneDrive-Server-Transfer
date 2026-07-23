using System.IO;
using System.Text;

namespace OneDriveServerTransfer.Reporting;

/// <summary>
/// CSV writer for the audit reports (docs/REPORT_SCHEMA.md, "CSV safety"). Encoding is
/// UTF-8 without a BOM. Escaping follows RFC 4180: a field containing a comma, quote,
/// CR, or LF is enclosed in quotes and embedded quotes are doubled.
///
/// Formula-injection policy (approved writer policy): any value whose first character
/// is '=', '+', '-', '@', a tab, a carriage return, or a line feed is neutralized with
/// a leading apostrophe before escaping, so spreadsheet applications never evaluate an
/// untrusted source value as a formula. Neutralization applies to every field, so an
/// untrusted value is safe regardless of which column carries it.
/// </summary>
internal static class CsvReportWriter
{
    /// <summary>The exact schema-version-1 header order for the item reports.</summary>
    public const string ItemsHeader =
        "ReportSchemaVersion,RunId,OperatorUPN,EmployeeUPN,SourceDriveId,SourceItemId," +
        "SourcePath,LocalPath,ItemType,SizeBytes,Status,AttemptCount,SourceHashType," +
        "SourceHashValue,LocalSha256,TimestampStatus,ErrorCode,ErrorMessage," +
        "StartedAtUtc,CompletedAtUtc";

    /// <summary>UTF-8 without a BOM; every report file uses this encoding.</summary>
    public static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes one CRLF-terminated row of escaped, neutralized fields.</summary>
    public static void WriteRow(TextWriter writer, IReadOnlyList<string?> fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write(Escape(fields[index]));
        }

        writer.Write("\r\n");
    }

    /// <summary>Neutralizes formula injection, then applies RFC 4180 escaping.</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var neutralized = NeutralizeFormulaInjection(value);
        return neutralized.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + neutralized.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : neutralized;
    }

    private static string NeutralizeFormulaInjection(string value) =>
        value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n'
            ? "'" + value
            : value;
}

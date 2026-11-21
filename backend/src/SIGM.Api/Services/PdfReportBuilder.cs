using System.Globalization;
using System.Text;

namespace SIGM.Api.Services;

/// <summary>
/// Generador de PDF mínimo (PDF 1.4, fuentes Helvetica) sin dependencias externas.
/// Se usa para exportar la historia clínica: solo texto, varias páginas, saltos y
/// estilos básicos. No pretende ser un motor de maquetación completo.
/// </summary>
public sealed class PdfReportBuilder
{
    public enum Style { Title, Heading, Body, Small, Spacer, Rule }

    private const double PageWidth = 595.28;   // A4 en puntos
    private const double PageHeight = 841.89;
    private const double MarginX = 42;
    private const double MarginTop = 46;
    private const double MarginBottom = 46;

    private readonly List<(string Text, Style Style)> _lines = new();
    private string _header = "";
    private string _footer = "";

    public PdfReportBuilder Header(string header) { _header = header; return this; }
    public PdfReportBuilder Footer(string footer) { _footer = footer; return this; }

    public PdfReportBuilder Add(string? text, Style style = Style.Body)
    {
        if (string.IsNullOrWhiteSpace(text)) return this;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            _lines.Add((raw, style));
        return this;
    }

    public PdfReportBuilder Title(string text) => Add(text, Style.Title);
    public PdfReportBuilder Heading(string text) => Add(text, Style.Heading);
    public PdfReportBuilder Body(string? text) => Add(text, Style.Body);
    public PdfReportBuilder Small(string? text) => Add(text, Style.Small);
    public PdfReportBuilder Spacer() { _lines.Add(("", Style.Spacer)); return this; }
    public PdfReportBuilder Rule() { _lines.Add(("", Style.Rule)); return this; }
    public PdfReportBuilder KeyValue(string key, string? value) => Body($"{key}: {value ?? "—"}");

    public byte[] Build()
    {
        var pages = Paginate();
        using var ms = new MemoryStream();
        var offsets = new List<long>();

        void Write(string s) => ms.Write(Encoding.Latin1.GetBytes(s));

        Write("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");

        int pageCount = pages.Count;
        int firstPageObj = 5; // 1 catálogo, 2 pages, 3/4 fuentes
        var pageObjNumbers = Enumerable.Range(0, pageCount).Select(i => firstPageObj + i * 2).ToArray();

        AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        AddObject(2, $"<< /Type /Pages /Kids [{string.Join(" ", pageObjNumbers.Select(n => $"{n} 0 R"))}] /Count {pageCount} >>");
        AddObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        AddObject(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        for (int i = 0; i < pageCount; i++)
        {
            var content = BuildContent(pages[i], i + 1, pageCount);
            int pageObj = pageObjNumbers[i];
            int contentObj = pageObj + 1;
            AddObject(pageObj, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] " +
                               $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObj} 0 R >>");
            AddStreamObject(contentObj, content);
        }

        long xrefPos = ms.Position;
        int total = offsets.Count + 1;
        var sb = new StringBuilder();
        sb.Append("xref\n0 ").Append(total).Append('\n');
        sb.Append("0000000000 65535 f \n");
        foreach (var off in offsets) sb.Append(off.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        sb.Append("trailer << /Size ").Append(total).Append(" /Root 1 0 R >>\nstartxref\n")
          .Append(xrefPos.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
        Write(sb.ToString());
        return ms.ToArray();

        void AddObject(int number, string body)
        {
            offsets.Add(ms.Position);
            Write($"{number} 0 obj\n{body}\nendobj\n");
        }

        void AddStreamObject(int number, string stream)
        {
            offsets.Add(ms.Position);
            var bytes = Encoding.Latin1.GetBytes(stream);
            Write($"{number} 0 obj\n<< /Length {bytes.Length} >>\nstream\n");
            ms.Write(bytes);
            Write("\nendstream\nendobj\n");
        }
    }

    private List<List<(string Text, Style Style)>> Paginate()
    {
        var pages = new List<List<(string, Style)>>();
        var current = new List<(string, Style)>();
        double y = PageHeight - MarginTop - 22;
        const double bottom = MarginBottom + 26;

        foreach (var line in _lines)
        {
            var wrapped = Wrap(line.Text, line.Style);
            foreach (var w in wrapped)
            {
                double h = LineHeight(w.Style);
                if (y - h < bottom)
                {
                    pages.Add(current);
                    current = new List<(string, Style)>();
                    y = PageHeight - MarginTop - 22;
                }
                current.Add((w.Text, w.Style));
                y -= h;
            }
        }
        if (current.Count > 0 || pages.Count == 0) pages.Add(current);
        return pages;
    }

    private string BuildContent(List<(string Text, Style Style)> lines, int pageNumber, int pageCount)
    {
        var sb = new StringBuilder();
        double y = PageHeight - MarginTop;

        if (!string.IsNullOrWhiteSpace(_header))
        {
            sb.Append(TextOp(_header, MarginX, y, 8, bold: true, gray: 0.35));
            y -= 14;
        }
        sb.Append($"0.80 0.84 0.88 RG 0.7 w {Num(MarginX)} {Num(y)} m {Num(PageWidth - MarginX)} {Num(y)} l S\n");
        y -= 16;

        foreach (var (text, style) in lines)
        {
            switch (style)
            {
                case Style.Spacer:
                    y -= 6;
                    break;
                case Style.Rule:
                    sb.Append($"0.86 0.89 0.92 RG 0.6 w {Num(MarginX)} {Num(y)} m {Num(PageWidth - MarginX)} {Num(y)} l S\n");
                    y -= 8;
                    break;
                case Style.Title:
                    sb.Append(TextOp(text, MarginX, y - 4, 16, bold: true, gray: 0));
                    y -= 22;
                    break;
                case Style.Heading:
                    sb.Append(TextOp(text, MarginX, y - 2, 10.5, bold: true, gray: 0.12));
                    y -= 16;
                    break;
                case Style.Small:
                    foreach (var part in Wrap(text, style))
                    {
                        sb.Append(TextOp(part.Text, MarginX, y, 8, bold: false, gray: 0.42));
                        y -= 11;
                    }
                    break;
                default:
                    foreach (var part in Wrap(text, style))
                    {
                        sb.Append(TextOp(part.Text, MarginX, y, 9.5, bold: false, gray: 0.1));
                        y -= 13;
                    }
                    break;
            }
        }

        var footer = string.IsNullOrWhiteSpace(_footer) ? "" : _footer + "  ·  ";
        sb.Append(TextOp($"{footer}Página {pageNumber} de {pageCount}", MarginX, MarginBottom, 8, bold: false, gray: 0.45));
        return sb.ToString();
    }

    private static string TextOp(string text, double x, double y, double size, bool bold, double gray)
        => $"{Num(gray)} g BT /{(bold ? "F2" : "F1")} {Num(size)} Tf {Num(x)} {Num(y)} Td ({Escape(text)}) Tj ET\n";

    private static IEnumerable<(string Text, Style Style)> Wrap(string text, Style style)
    {
        if (string.IsNullOrEmpty(text)) { yield return ("", style); yield break; }
        int max = style switch
        {
            Style.Title => 60,
            Style.Heading => 92,
            Style.Small => 118,
            _ => 100
        };
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (candidate.Length > max && current.Length > 0)
            {
                yield return (current.ToString(), style);
                current.Clear();
                current.Append(word);
            }
            else
            {
                current.Clear();
                current.Append(candidate);
            }
        }
        if (current.Length > 0) yield return (current.ToString(), style);
    }

    private static double LineHeight(Style style) => style switch
    {
        Style.Title => 22,
        Style.Heading => 16,
        Style.Small => 11,
        Style.Spacer => 6,
        Style.Rule => 8,
        _ => 13
    };

    private static string Escape(string input)
    {
        var sb = new StringBuilder(input.Length + 8);
        foreach (var ch in input)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\u2013': sb.Append('-'); break;
                case '\u2014': sb.Append('-'); break;
                case '\u2018': case '\u2019': sb.Append('\''); break;
                case '\u201c': case '\u201d': sb.Append('"'); break;
                case '\u2022': sb.Append('-'); break;
                case '\n': case '\r': break;
                default:
                    if (ch < 32) break;
                    sb.Append(ch <= 0xFF ? ch : '?');
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

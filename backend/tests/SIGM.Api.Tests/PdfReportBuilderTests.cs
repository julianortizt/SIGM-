using System.Text;
using System.Text.RegularExpressions;
using SIGM.Api.Services;

namespace SIGM.Api.Tests;

public class PdfReportBuilderTests
{
    [Fact]
    public void Build_GeneraPdfValido()
    {
        var pdf = new PdfReportBuilder()
            .Header("SIGM Cloud")
            .Footer("Historia clínica")
            .Title("Historia clínica")
            .KeyValue("Paciente", "Ana Pérez")
            .Heading("Atenciones")
            .Body("Consulta de control")
            .Build();

        var text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("Ana P", text);
        Assert.Contains("startxref", text);
    }

    [Fact]
    public void Build_EscapaParentesisYBarraInvertida()
    {
        var pdf = new PdfReportBuilder().Body("Dolor (leve) \\ control").Build();
        var text = Encoding.Latin1.GetString(pdf);

        Assert.Contains(@"Dolor \(leve\) \\ control", text);
    }

    [Fact]
    public void Build_TextoLargo_GeneraVariasPaginas()
    {
        var builder = new PdfReportBuilder().Title("Reporte");
        for (int i = 0; i < 200; i++) builder.Body($"Línea de evolución número {i} con contenido clínico de prueba.");

        var text = Encoding.Latin1.GetString(builder.Build());
        var count = Regex.Match(text, @"/Type /Pages /Kids \[[^\]]*\] /Count (\d+)");

        Assert.True(count.Success);
        Assert.True(int.Parse(count.Groups[1].Value) > 1);
        Assert.Contains("Página 2 de", text);
    }

    [Fact]
    public void Build_SinContenido_GeneraUnaPagina()
    {
        var text = Encoding.Latin1.GetString(new PdfReportBuilder().Build());

        Assert.Contains("/Count 1", text);
    }
}

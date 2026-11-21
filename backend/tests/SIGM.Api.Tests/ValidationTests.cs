using SIGM.Api.Validation;

namespace SIGM.Api.Tests;

public class TenantReferenceTests
{
    [Fact]
    public void Parse_Uuid_DevuelveId()
    {
        var (id, code) = TenantReference.Parse("  11111111-1111-4111-8111-111111111111  ");

        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), id);
        Assert.Null(code);
    }

    [Fact]
    public void Parse_Codigo_DevuelveCodigoEnMayusculas()
    {
        var (id, code) = TenantReference.Parse(" clinic-demo ");

        Assert.Null(id);
        Assert.Equal("CLINIC-DEMO", code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Vacio_DevuelveNada(string? raw)
    {
        var (id, code) = TenantReference.Parse(raw);

        Assert.Null(id);
        Assert.Null(code);
    }
}

public class PatientRulesTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 14);

    [Fact]
    public void Validate_DatosMinimos_Acepta()
    {
        Assert.Null(PatientRules.Validate("Ana", "Pérez", null, null, null, null, null, Hoy));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_SinNombre_Rechaza(string? nombre)
    {
        Assert.NotNull(PatientRules.Validate(nombre, "Pérez", null, null, null, null, null, Hoy));
    }

    [Theory]
    [InlineData("PRUEBA BUFFY")]
    [InlineData("sin-arroba.test")]
    [InlineData("a@b")]
    public void Validate_CorreoInvalido_Rechaza(string correo)
    {
        Assert.NotNull(PatientRules.Validate("Ana", "Pérez", correo, null, null, null, null, Hoy));
    }

    [Fact]
    public void Validate_CorreoValido_Acepta()
    {
        Assert.Null(PatientRules.Validate("Ana", "Pérez", "ana.perez@ejemplo.test", null, null, null, null, Hoy));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    public void Validate_TelefonoInvalido_Rechaza(string telefono)
    {
        Assert.NotNull(PatientRules.Validate("Ana", "Pérez", null, telefono, null, null, null, Hoy));
    }

    [Fact]
    public void Validate_FechaNacimientoFutura_Rechaza()
    {
        var error = PatientRules.Validate("Ana", "Pérez", null, null, null, Hoy.AddDays(1), null, Hoy);

        Assert.NotNull(error);
        Assert.Contains("futura", error);
    }

    [Fact]
    public void Validate_DocumentoLargo_Rechaza()
    {
        var error = PatientRules.Validate("Ana", "Pérez", null, null, null, null, new string('9', 41), Hoy);

        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("M", "M")]
    [InlineData("f", "F")]
    [InlineData("INTERSEX", "INTERSEX")]
    [InlineData("otro", "UNKNOWN")]
    [InlineData(null, "UNKNOWN")]
    public void NormalizeSex_SoloValoresPermitidos(string? entrada, string esperado)
    {
        Assert.Equal(esperado, PatientRules.NormalizeSex(entrada));
    }

    [Fact]
    public void Clean_RecortaYVaciaANull()
    {
        Assert.Equal("Bogotá", PatientRules.Clean("  Bogotá "));
        Assert.Null(PatientRules.Clean("   "));
        Assert.Null(PatientRules.Clean(null));
    }
}

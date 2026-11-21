using System.Text.RegularExpressions;

namespace SIGM.Api.Validation;

/// <summary>
/// Reglas de validación de pacientes que se aplican antes de tocar la base de datos.
/// La BD tiene CHECK/UNIQUE propios; validar aquí permite responder 400/409 con un
/// mensaje entendible en lugar de un 500 por excepción de PostgreSQL.
/// </summary>
public static class PatientRules
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 200;
    public const int MaxDocumentLength = 40;

    private static readonly Regex EmailFormat = new(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PhoneFormat = new(@"^[0-9+\-() ]{6,40}$", RegexOptions.Compiled);

    /// <summary>Motivo del rechazo, o <c>null</c> si los datos son aceptables.</summary>
    public static string? Validate(
        string? firstName,
        string? lastName,
        string? email,
        string? phone,
        string? mobilePhone,
        DateOnly? birthDate,
        string? documentNumber,
        DateOnly? today = null)
    {
        if (string.IsNullOrWhiteSpace(firstName)) return "El nombre del paciente es obligatorio.";
        if (firstName.Trim().Length > MaxNameLength) return $"El nombre no puede superar {MaxNameLength} caracteres.";
        if (string.IsNullOrWhiteSpace(lastName)) return "El apellido del paciente es obligatorio.";
        if (lastName.Trim().Length > MaxNameLength) return $"El apellido no puede superar {MaxNameLength} caracteres.";

        var mail = email?.Trim();
        if (!string.IsNullOrEmpty(mail))
        {
            if (mail.Length > MaxEmailLength) return $"El correo no puede superar {MaxEmailLength} caracteres.";
            if (!EmailFormat.IsMatch(mail)) return "El correo electrónico no tiene un formato válido.";
        }

        var tel = phone?.Trim();
        if (!string.IsNullOrEmpty(tel) && !PhoneFormat.IsMatch(tel))
            return "El teléfono solo admite números, espacios, +, - y paréntesis (entre 6 y 40 caracteres).";

        var cel = mobilePhone?.Trim();
        if (!string.IsNullOrEmpty(cel) && !PhoneFormat.IsMatch(cel))
            return "El celular solo admite números, espacios, +, - y paréntesis (entre 6 y 40 caracteres).";

        var doc = documentNumber?.Trim();
        if (!string.IsNullOrEmpty(doc) && doc.Length > MaxDocumentLength)
            return $"El número de documento no puede superar {MaxDocumentLength} caracteres.";

        var limit = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (birthDate is DateOnly bd && bd > limit) return "La fecha de nacimiento no puede ser futura.";

        return null;
    }

    /// <summary>Recorta el texto y convierte cadenas vacías en <c>null</c>.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Normaliza el sexo al conjunto aceptado por la base de datos.</summary>
    public static string NormalizeSex(string? sex)
    {
        var value = sex?.Trim().ToUpperInvariant();
        return value is "M" or "F" or "INTERSEX" ? value : "UNKNOWN";
    }
}

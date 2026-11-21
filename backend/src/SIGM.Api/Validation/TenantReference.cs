namespace SIGM.Api.Validation;

/// <summary>
/// Referencia a una organización escrita por el usuario. Se acepta el UUID o el
/// código legible (tenant_code) para que nadie tenga que memorizar un GUID.
/// </summary>
public static class TenantReference
{
    /// <summary>
    /// Devuelve <c>(guid, null)</c> si el valor es un UUID, <c>(null, código)</c> si es
    /// un código y <c>(null, null)</c> cuando viene vacío.
    /// </summary>
    public static (Guid? Id, string? Code) Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, null);
        var value = raw.Trim();
        return Guid.TryParse(value, out var id) ? (id, null) : (null, value.ToUpperInvariant());
    }
}

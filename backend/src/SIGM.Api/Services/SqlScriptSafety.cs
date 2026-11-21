using System.Text.RegularExpressions;

namespace SIGM.Api.Services;

/// <summary>
/// Análisis estático de scripts SQL cargados desde el panel de plataforma.
/// No sustituye a un entorno aislado: es una barrera mínima que rechaza las
/// sentencias más peligrosas o incompatibles con una ejecución transaccional y
/// marca las destructivas para exigir confirmación reforzada.
/// </summary>
public static partial class SqlScriptSafety
{
    public sealed record Analysis(bool Allowed, bool Destructive, int Statements, string? Reason, IReadOnlyList<string> Warnings);

    private static readonly (string Pattern, string Reason)[] Blocked =
    [
        (@"\bdrop\s+database\b", "No se permite DROP DATABASE desde el panel."),
        (@"\balter\s+system\b", "No se permite ALTER SYSTEM desde el panel."),
        (@"\bcopy\b[\s\S]*?\bprogram\b", "No se permite COPY ... PROGRAM (ejecución de comandos del sistema)."),
        (@"\bcreate\s+extension\b", "Las extensiones se instalan por aprovisionamiento, no desde el panel."),
        (@"\b(drop|create|alter)\s+role\b", "La gestión de roles se realiza por aprovisionamiento."),
        (@"\b(set|reset)\s+role\b", "No se permite cambiar de rol dentro del script."),
        (@"\bgrant\b", "Los GRANT/REVOKE se gestionan por migración aprobada, no desde el panel."),
        (@"\brevoke\b", "Los GRANT/REVOKE se gestionan por migración aprobada, no desde el panel."),
        (@"\bpg_read_file\b|\bpg_ls_dir\b|\blo_import\b|\blo_export\b", "Funciones de acceso al sistema de archivos no permitidas."),
        (@"\bcreate\s+(unique\s+)?index\s+concurrently\b", "CREATE INDEX CONCURRENTLY no puede ejecutarse dentro de una transacción."),
        (@"\bvacuum\b", "VACUUM no puede ejecutarse dentro de una transacción."),
        (@"\breindex\b", "REINDEX no puede ejecutarse dentro de una transacción."),
        (@"\bcreate\s+database\b", "No se permite CREATE DATABASE desde el panel."),
        (@"\bcommit\b|\brollback\b|\bbegin\b|\bstart\s+transaction\b", "El script no debe abrir ni cerrar transacciones: el panel las controla."),
    ];

    private static readonly (string Pattern, string Reason)[] Destructive =
    [
        (@"\bdrop\s+(table|schema|column|index|type|view|materialized\s+view|sequence)\b", "Contiene DROP de objetos."),
        (@"\btruncate\b", "Contiene TRUNCATE."),
        (@"\balter\s+table\b[\s\S]*?\bdrop\b", "Contiene ALTER TABLE ... DROP."),
    ];

    public static Analysis Analyze(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new Analysis(false, false, 0, "El script está vacío.", []);

        var warnings = new List<string>();
        var clean = StripComments(sql);

        foreach (var line in clean.Split('\n'))
            if (line.TrimStart().StartsWith('\\'))
                return new Analysis(false, false, 0, "El script contiene meta-comandos de psql (\\...). Use SQL estándar.", warnings);

        if (Regex.IsMatch(clean, @"\bcopy\b[\s\S]*?\bfrom\s+stdin\b", RegexOptions.IgnoreCase))
            return new Analysis(false, false, 0, "COPY ... FROM stdin no es compatible con la ejecución por API. Use INSERT.", warnings);

        foreach (var (pattern, reason) in Blocked)
            if (Regex.IsMatch(clean, pattern, RegexOptions.IgnoreCase))
                return new Analysis(false, false, 0, reason, warnings);

        var destructive = false;
        foreach (var (pattern, reason) in Destructive)
        {
            if (Regex.IsMatch(clean, pattern, RegexOptions.IgnoreCase))
            {
                destructive = true;
                warnings.Add(reason);
            }
        }

        foreach (var statement in SplitStatements(clean))
        {
            if (Regex.IsMatch(statement, @"^\s*delete\s+from\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(statement, @"\bwhere\b", RegexOptions.IgnoreCase))
            {
                destructive = true;
                warnings.Add("DELETE sin WHERE afecta toda la tabla.");
            }
            if (Regex.IsMatch(statement, @"^\s*update\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(statement, @"\bwhere\b", RegexOptions.IgnoreCase))
            {
                destructive = true;
                warnings.Add("UPDATE sin WHERE afecta toda la tabla.");
            }
        }

        var statements = SplitStatements(clean).Count;
        if (statements == 0)
            return new Analysis(false, false, 0, "No se detectaron sentencias SQL ejecutables.", warnings);

        return new Analysis(true, destructive, statements, null, warnings);
    }

    public static List<string> SplitStatements(string sql)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inSingle = false;
        var inDouble = false;
        string? dollarTag = null;

        for (int i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];

            if (dollarTag is not null)
            {
                if (ch == '$' && string.CompareOrdinal(sql, i, dollarTag, 0, dollarTag.Length) == 0)
                {
                    current.Append(dollarTag);
                    i += dollarTag.Length - 1;
                    dollarTag = null;
                    continue;
                }
                current.Append(ch);
                continue;
            }

            if (!inSingle && !inDouble && ch == '$')
            {
                var end = sql.IndexOf('$', i + 1);
                if (end > i && end - i <= 40)
                {
                    var tag = sql[i..(end + 1)];
                    if (tag.Skip(1).SkipLast(1).All(c => char.IsLetterOrDigit(c) || c == '_'))
                    {
                        dollarTag = tag;
                        current.Append(tag);
                        i = end;
                        continue;
                    }
                }
            }

            if (ch == '\'' && !inDouble) inSingle = !inSingle;
            else if (ch == '"' && !inSingle) inDouble = !inDouble;

            if (ch == ';' && !inSingle && !inDouble)
            {
                var text = current.ToString().Trim();
                if (text.Length > 0) result.Add(text);
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        var tail = current.ToString().Trim();
        if (tail.Length > 0) result.Add(tail);
        return result;
    }

    private static string StripComments(string sql)
    {
        var noBlock = Regex.Replace(sql, @"/\*[\s\S]*?\*/", " ");
        return Regex.Replace(noBlock, @"--[^\n]*", " ");
    }
}

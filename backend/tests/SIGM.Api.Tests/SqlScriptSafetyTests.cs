using SIGM.Api.Services;

namespace SIGM.Api.Tests;

public class SqlScriptSafetyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Analyze_Vacio_Rechaza(string? sql)
    {
        var result = SqlScriptSafety.Analyze(sql!);

        Assert.False(result.Allowed);
        Assert.Contains("vacío", result.Reason);
    }

    [Fact]
    public void Analyze_InsertSimple_Permite()
    {
        var result = SqlScriptSafety.Analyze("INSERT INTO sigm.insurers (name) VALUES ('Sura');");

        Assert.True(result.Allowed);
        Assert.False(result.Destructive);
        Assert.Equal(1, result.Statements);
    }

    [Theory]
    [InlineData("DROP DATABASE sigm_cloud_dev;")]
    [InlineData("CREATE EXTENSION pgcrypto;")]
    [InlineData("GRANT ALL ON SCHEMA sigm TO PUBLIC;")]
    [InlineData("ALTER SYSTEM SET work_mem = '1GB';")]
    [InlineData("VACUUM FULL sigm.patients;")]
    [InlineData("BEGIN; INSERT INTO sigm.insurers (name) VALUES ('X'); COMMIT;")]
    [InlineData("COPY sigm.patients FROM stdin;")]
    public void Analyze_SentenciaPeligrosa_Rechaza(string sql)
    {
        var result = SqlScriptSafety.Analyze(sql);

        Assert.False(result.Allowed);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void Analyze_MetaComandoPsql_Rechaza()
    {
        var result = SqlScriptSafety.Analyze("\\copy sigm.patients TO '/tmp/p.csv'");

        Assert.False(result.Allowed);
        Assert.Contains("meta-comandos", result.Reason);
    }

    [Theory]
    [InlineData("DROP TABLE sigm.tmp_backup;")]
    [InlineData("TRUNCATE sigm.staging_patients;")]
    [InlineData("ALTER TABLE sigm.patients DROP COLUMN legacy_code;")]
    public void Analyze_Destructivo_PermitePeroMarca(string sql)
    {
        var result = SqlScriptSafety.Analyze(sql);

        Assert.True(result.Allowed);
        Assert.True(result.Destructive);
    }

    [Fact]
    public void Analyze_Comentarios_NoConfundenCorchetes()
    {
        var result = SqlScriptSafety.Analyze("-- DROP DATABASE no se ejecuta\nINSERT INTO sigm.insurers (name) VALUES ('Ok');");

        Assert.True(result.Allowed);
        Assert.False(result.Destructive);
    }
}

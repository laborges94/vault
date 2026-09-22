using Vault.Cli.Output;

namespace Vault.Cli.Tests.Output;

public class ConsoleFormatterTests
{
    [Fact]
    public void WriteRaw_WritesExactTextWithoutNewline()
    {
        var outWriter = new StringWriter();
        var formatter = new ConsoleFormatter(output: outWriter);

        formatter.WriteRaw("secret_token_value_raw");

        Assert.Equal("secret_token_value_raw", outWriter.ToString());
    }

    [Fact]
    public void WriteWarningAndError_WritesToErrorStreamWithPrefix()
    {
        var errWriter = new StringWriter();
        var formatter = new ConsoleFormatter(error: errWriter);

        formatter.WriteWarning("This is a warning");
        formatter.WriteError("This is an error");

        var text = errWriter.ToString();
        Assert.Contains("Warning: This is a warning", text);
        Assert.Contains("Error: This is an error", text);
    }

    [Fact]
    public void WriteTable_RendersHeadersSeparatorsAndAlignedRows()
    {
        var outWriter = new StringWriter();
        var formatter = new ConsoleFormatter(output: outWriter);

        var data = new[]
        {
            new { Key = "DATABASE_URL", Tag = "db" },
            new { Key = "SHORT", Tag = "production,api" }
        };

        formatter.WriteTable(
            data,
            ("KEY", x => x.Key),
            ("TAGS", x => x.Tag));

        var output = outWriter.ToString();

        Assert.Contains("KEY", output);
        Assert.Contains("TAGS", output);
        Assert.Contains("DATABASE_URL", output);
        Assert.Contains("production,api", output);
    }
}

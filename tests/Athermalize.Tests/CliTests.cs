using Athermalize.Cli;
using Xunit;

namespace Athermalize.Tests;

public class CliTests
{
    private static (int Code, string Text) Run(params string[] args)
    {
        var w = new StringWriter();
        int code = Program.Run(args, w);
        return (code, w.ToString());
    }

    [Fact]
    public void Materials_prints_each_glass()
    {
        var (code, text) = Run("materials", "SCHOTT:N-BK7", "SCHOTT:SF66", "--catalogs", Catalogs.Folder,
            "--temperatures", "20", "--wavelengths", "0.5875618");
        Assert.Equal(0, code);
        Assert.Contains("SCHOTT:N-BK7: thermal data present, Tref 20 °C", text);
        Assert.Contains("1.5168000", text);
        Assert.Contains("SCHOTT:SF66: thermal data missing; no thermal coefficients, so only the air changes", text);
    }

    [Fact]
    public void Materials_lists_the_measured_materials()
    {
        var (code, text) = Run("materials", "--measured", "--catalogs", Catalogs.Folder);
        Assert.Equal(0, code);
        Assert.Contains("CHARMS:SI", text);
        Assert.Contains("doi:10.1117/12.672850", text);
    }

    [Fact]
    public void Unknown_material_fails()
    {
        Assert.Equal(1, Run("materials", "NO_SUCH_GLASS", "--catalogs", Catalogs.Folder).Code);
    }
}

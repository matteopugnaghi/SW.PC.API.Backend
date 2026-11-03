using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Tests;

/// <summary>
/// Hoja TrainRecipe: configuración del PLC alternativo (receta 2).
///   A13 = ON/OFF habilita, A14 = prefijo (sustituye "st_TrainRecipe"), A15 = OFF excluye tablas Gantry.
/// Misma semántica que WashRecipe (A13/A14). Antes sólo se leía A14 y cualquier texto activaba PLC2.
/// </summary>
public class TrainRecipeAlternatePlcConfigTests
{
    [Fact]
    public async Task A13_On_WithPrefix_EnablesAlternate_GantryIncludedByDefault()
    {
        var cfg = await Load(a13: "ON", a14: "st_TrainRecipe2", a15: null);

        Assert.True(cfg.AlternateWriteEnabled);
        Assert.Equal("st_TrainRecipe2", cfg.AlternatePlcPrefix);
        Assert.True(cfg.AlternateIncludeGantryTables);
    }

    [Fact]
    public async Task A15_Off_ExcludesGantryTables()
    {
        var cfg = await Load(a13: "on", a14: "st_TrainRecipe2", a15: "off");

        Assert.True(cfg.AlternateWriteEnabled);
        Assert.False(cfg.AlternateIncludeGantryTables);
    }

    [Fact]
    public async Task A14_WithoutA13_DoesNotEnableAlternate()
    {
        var cfg = await Load(a13: null, a14: "st_TrainRecipe2", a15: null);

        Assert.False(cfg.AlternateWriteEnabled);
        Assert.Equal("st_TrainRecipe2", cfg.AlternatePlcPrefix);
    }

    [Fact]
    public async Task A13_On_WithoutPrefix_IsDisabled()
    {
        var cfg = await Load(a13: "ON", a14: null, a15: null);

        Assert.False(cfg.AlternateWriteEnabled);
        Assert.Null(cfg.AlternatePlcPrefix);
    }

    [Fact]
    public async Task Wash_A13_A14_StillWork()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alt-wash-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var wash = workbook.AddWorksheet("WashRecipe");
                wash.Cell("A3").Value = "MAIN.st_WashRecipe.s_Name";
                wash.Cell("A13").Value = "ON";
                wash.Cell("A14").Value = "st_WashRecipe2";
                workbook.SaveAs(path);
            }
            var cfg = await CreateService().LoadWashRecipeConfigAsync(path);
            Assert.True(cfg.AlternateWriteEnabled);
            Assert.Equal("st_WashRecipe2", cfg.AlternateWritePlcPrefix);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<Models.TrainRecipeConfiguration> Load(string? a13, string? a14, string? a15)
    {
        var path = Path.Combine(Path.GetTempPath(), $"alt-train-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var train = workbook.AddWorksheet("TrainRecipe");
                train.Cell("A3").Value = "MAIN.fbMachine.st_TrainRecipe[1].s_Name";
                train.Cell("C2").Value = "Enable";
                train.Cell("E2").Value = "MAIN.fbMachine.st_TrainRecipe[1].b_Enable";
                if (a13 != null) train.Cell("A13").Value = a13;
                if (a14 != null) train.Cell("A14").Value = a14;
                if (a15 != null) train.Cell("A15").Value = a15;
                workbook.SaveAs(path);
            }
            return await CreateService().LoadTrainRecipeConfigAsync(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ExcelConfigService CreateService()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());
        environment.SetupGet(e => e.EnvironmentName).Returns("Production");
        return new ExcelConfigService(environment.Object, NullLogger<ExcelConfigService>.Instance, Mock.Of<IMetricsService>());
    }
}

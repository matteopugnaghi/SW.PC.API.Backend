using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Tests;

public class ExcelDecimalPrecisionTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("null", 0)]
    [InlineData(" NULL ", 0)]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("3", 3)]
    [InlineData("100", 100)]
    [InlineData("#0", 0)]
    [InlineData(" #0 ", 0)]
    public async Task AllDecimalColumnsUseTheSameCount(string? text, int expected)
    {
        using var workbook = CreateWorkbook();
        foreach (var cell in DecimalCells(workbook))
            if (text != null) cell.Value = text;

        await AssertCounts(workbook, expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task NumericCellsIgnoreExcelVisualNumberFormat(int expected)
    {
        using var workbook = CreateWorkbook();
        foreach (var cell in DecimalCells(workbook))
        {
            cell.Value = expected;
            cell.Style.NumberFormat.Format = "0.000";
        }
        await AssertCounts(workbook, expected);
    }

    [Theory]
    [InlineData("setting page", "R2")]
    [InlineData("setting page", "Z2")]
    [InlineData("TrainRecipe", "L2")]
    [InlineData("TrainRecipe", "T2")]
    [InlineData("Fast_Configuration", "S2")]
    public async Task InvalidCountsAreRejectedWithCellLocation(string sheet, string address)
    {
        foreach (var invalid in new[] { "-1", "2.5", "101", "abc" })
        {
            using var workbook = CreateWorkbook();
            workbook.Worksheet(sheet).Cell(address).Value = invalid;
            var path = Path.Combine(Path.GetTempPath(), $"decimal-test-{Guid.NewGuid():N}.xlsx");
            try
            {
                workbook.SaveAs(path);
                var service = CreateService();
                var exception = await Assert.ThrowsAsync<FormatException>(async () =>
                {
                    if (sheet == "setting page") await service.LoadSettingsPageAsync(path);
                    else if (sheet == "TrainRecipe") await service.LoadTrainRecipeConfigAsync(path);
                    else await service.LoadFastConfigurationAsync(path);
                });
                Assert.Contains($"{sheet}!{address}", exception.Message);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ModelDefaultsAreZero()
    {
        Assert.Equal(0, new LongRealSettingParameter().DecimalPlaces);
        Assert.Equal(0, new ExcelLongRealSetting().DecimalPlaces);
        Assert.Equal(0, new FastConfigLRealSetting().DecimalPlaces);
        Assert.Equal(0, new GantryConfigParameter().Decimals);
    }

    [Fact]
    public async Task LegacyMasksWarnWithTheSheetAndCellInsteadOfBlockingThePage()
    {
        using var workbook = CreateWorkbook();
        foreach (var cell in DecimalCells(workbook)) cell.Value = "#0";
        var logger = new Mock<ILogger<ExcelConfigService>>();
        await AssertCounts(workbook, 0, logger.Object);
        var warnings = logger.Invocations
            .Where(i => i.Method.Name == "Log" && (LogLevel)i.Arguments[0] == LogLevel.Warning)
            .Select(i => i.Arguments[2].ToString() ?? string.Empty)
            .ToList();
        foreach (var cell in DecimalCells(workbook))
            Assert.Contains(warnings, message =>
                message.Contains("Legacy decimal format #0") &&
                message.Contains($"{cell.Worksheet.Name}!{cell.Address}"));
    }

    private static async Task AssertCounts(XLWorkbook workbook, int expected, ILogger<ExcelConfigService>? logger = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"decimal-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            workbook.SaveAs(path);
            var service = CreateService(logger);
            var settings = await service.LoadSettingsPageAsync(path);
            var train = await service.LoadTrainRecipeConfigAsync(path);
            var fast = await service.LoadFastConfigurationAsync(path);
            Assert.Equal(expected, Assert.Single(settings.LongRealSettings).DecimalPlaces);
            Assert.Equal(expected, Assert.Single(settings.LongReal2Settings).DecimalPlaces);
            Assert.Equal(expected, Assert.Single(train.DecimalParameters).Decimals);
            Assert.Equal(expected, Assert.Single(train.GantryConfigParameters).Decimals);
            Assert.Equal(expected, Assert.Single(fast.LRealSettings).DecimalPlaces);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ExcelConfigService CreateService(ILogger<ExcelConfigService>? logger = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());
        environment.SetupGet(e => e.EnvironmentName).Returns("Production");
        return new ExcelConfigService(environment.Object,
            logger ?? NullLogger<ExcelConfigService>.Instance, Mock.Of<IMetricsService>());
    }

    private static XLWorkbook CreateWorkbook()
    {
        var workbook = new XLWorkbook();
        var settings = workbook.AddWorksheet("setting page");
        settings.Cell("M2").Value = "First decimal";
        settings.Cell("O2").Value = "MAIN.first";
        settings.Cell("U2").Value = "Second decimal";
        settings.Cell("W2").Value = "MAIN.second";
        var train = workbook.AddWorksheet("TrainRecipe");
        train.Cell("G2").Value = "Train decimal";
        train.Cell("I2").Value = "MAIN.train";
        train.Cell("O2").Value = "Gantry decimal";
        train.Cell("Q2").Value = "MAIN.gantry";
        train.Cell("V2").Value = "TAB1_FW_UP";
        var fast = workbook.AddWorksheet("Fast_Configuration");
        fast.Cell("N2").Value = "Fast decimal";
        fast.Cell("P2").Value = "MAIN.fast";
        return workbook;
    }

    private static IEnumerable<IXLCell> DecimalCells(XLWorkbook workbook)
    {
        yield return workbook.Worksheet("setting page").Cell("R2");
        yield return workbook.Worksheet("setting page").Cell("Z2");
        yield return workbook.Worksheet("TrainRecipe").Cell("L2");
        yield return workbook.Worksheet("TrainRecipe").Cell("T2");
        yield return workbook.Worksheet("Fast_Configuration").Cell("S2");
    }
}

using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Tests.Services;

public class WashTypeParameterSynchronizerTests
{
    private const string V1 = "MAIN.fbMachine.st_WashRecipe.st_Station[1].b_Enable";
    private const string V2 = "MAIN.fbMachine.st_WashRecipe.st_Station[1].n_Time";

    private static PlcParameterData Plc(string code, string var, string value, string type)
        => new() { Code = code, Name = code, DataType = type, Value = value, PlcVariable = var, DisplayOrder = 1 };

    [Fact]
    public void Sync_MatchesByVariable_UpdatesAndRemovesOrphans()
    {
        var kept = new WashTypeParameter { ParameterCode = "BOOL_1_0", PlcVariable = V1, Value = "false", DataType = "BOOL" };
        var orphan = new WashTypeParameter { ParameterCode = "INT_9_9", PlcVariable = "MAIN.old", Value = "5", DataType = "INT" };
        var existing = new List<WashTypeParameter> { kept, orphan };

        var result = WashTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc("BOOL_1_0", V1, "true", "BOOL"),
            Plc("INT_1_0", V2, "30", "INT"),
        });

        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Added);
        Assert.Same(orphan, Assert.Single(result.Removed));
        Assert.Equal(2, existing.Count);
        Assert.Equal("true", kept.Value);
        Assert.Equal("30", existing.Single(p => p.PlcVariable == V2).Value);
    }

    [Fact]
    public void GetWritable_FiltersByExcel_AndWarnsMissing()
    {
        var cfg = new WashRecipeEditorConfiguration();
        var station = new WashRecipeStation();
        station.BoolParameters.Add(new WashRecipeBoolParam { PlcVariable = V1, Description = "Enable" });
        station.IntParameters.Add(new WashRecipeIntParam { PlcVariable = V2, Description = "Time" });
        cfg.Stations.Add(station);

        var parameters = new List<WashTypeParameter>
        {
            new() { Name = "Enable", PlcVariable = V1, Value = "true", DataType = "BOOL" },
            new() { Name = "Old", PlcVariable = "MAIN.old", Value = "1", DataType = "INT" },
        };

        var writable = WashTypeParameterSynchronizer.GetWritableParameters(parameters, cfg, out var warnings);

        Assert.Equal(V1, Assert.Single(writable).PlcVariable);
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("N_TIME") && w.Contains("re-guardar"));
        Assert.Contains(warnings, w => w.Contains("MAIN.old") && w.Contains("ignorado"));
    }

    [Theory]
    [InlineData("INT", "30", 30)]
    [InlineData("INT", "30,0", 30)]
    [InlineData("INT", "29.6", 30)]
    public void ToPlcValue_Int(string type, string value, int expected)
    {
        var (v, t) = WashTypeParameterSynchronizer.ToPlcValue(new WashTypeParameter { DataType = type, Value = value });
        Assert.Equal(typeof(int), t);
        Assert.Equal(expected, v);
    }

    [Fact]
    public void ToPlcValue_Bool()
    {
        var (v, t) = WashTypeParameterSynchronizer.ToPlcValue(new WashTypeParameter { DataType = "BOOL", Value = "True" });
        Assert.Equal(typeof(bool), t);
        Assert.Equal(true, v);
    }
}

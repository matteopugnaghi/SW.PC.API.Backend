using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Tests de la carga DB → PLC de un tipo de tren completo (parámetros + tablas Gantry),
/// verificada por relectura. Cubre las carencias detectadas en la auditoría:
///   - min_height / max_height no se escribían al cargar un slot
///   - FunctionType del punto END de la última línea no se escribía
///   - escrituras rechazadas por el driver contadas como OK
///   - ningún aviso cuando una tabla configurada en Excel no tenía datos en DB
/// </summary>
public class TrainTypePlcWriterTests
{
    private const string P = "MAIN.fbMachine.st_TrainRecipe[1]";

    private static (TrainTypePlcWriter Writer, FakePlc Plc, Mock<IOperationLogService> OpLog) Create()
    {
        var plc = new FakePlc();
        var opLog = new Mock<IOperationLogService>();
        opLog.Setup(o => o.LogAsync(It.IsAny<OperationCategory>(), It.IsAny<OperationAction>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new OperationLog());
        var verifier = new PlcWriteVerificationService(plc.Service, opLog.Object, NullLogger<PlcWriteVerificationService>.Instance);
        var writer = new TrainTypePlcWriter(verifier, NullLogger<TrainTypePlcWriter>.Instance);
        return (writer, plc, opLog);
    }

    private static TrainRecipeConfiguration Config(bool withTable = true)
    {
        var cfg = new TrainRecipeConfiguration
        {
            TrainNamePlcVariable = $"{P}.s_Name",
            LineNumberPlcVariable = $"{P}.n_Line",
            GantryTableCountPlcVariable = $"{P}.n_TableCount",
            WriteTriggerPlcVariable = $"{P}.b_Write",
            AlternatePlcPrefix = "st_TrainRecipe2"
        };
        cfg.BoolParameters.Add(new TrainRecipeParameter { Name = "Enable", PlcVariable = $"{P}.b_Enable_001", DataType = "BOOL" });
        cfg.DecimalParameters.Add(new TrainRecipeParameter { Name = "Speed", PlcVariable = $"{P}.r_Speed_003", DataType = "LREAL" });
        cfg.GantryConfigParameters.Add(new GantryConfigParameter { Name = "TypeOfWash", PlcVariable = $"{P}.r_TypeOfwash_012", Visibility = "TAB1_FW_UP" });

        if (withTable)
        {
            cfg.GantryInterpolationTables.Add(new GantryInterpolationTable
            {
                TableId = "TAB1_FW_UP",
                TableIndex = 0,
                LineCountPlcVariable = $"{P}.st_FwUp.n_Lines",
                MinHeightPlcVariable = $"{P}.st_FwUp.min_height",
                MaxHeightPlcVariable = $"{P}.st_FwUp.max_height",
                FunctionTypePlcTemplate = $"{P}.st_FwUp.st_Points[{{index}}].FunctionType",
                PositionXPlcTemplate = $"{P}.st_FwUp.st_Points[{{index}}].Position_X",
                PositionYPlcTemplate = $"{P}.st_FwUp.st_Points[{{index}}].Position_Y",
                SpeedYPlcTemplate = $"{P}.st_FwUp.st_Points[{{index}}].Speed_Y"
            });
        }
        return cfg;
    }

    private static TrainType TrainType(bool withGantryRows = true)
    {
        var tt = new TrainType { Id = 7, Code = "TRAIN_03", Name = "Metro", DisplayOrder = 3, GantryTableCount = 1 };
        tt.Parameters.Add(new TrainTypeParameter { Name = "Enable", DataType = "BOOL", Value = "true", PlcVariable = $"{P}.b_Enable_001", GroupName = "Bool" });
        tt.Parameters.Add(new TrainTypeParameter { Name = "Speed", DataType = "LREAL", Value = "12.5", PlcVariable = $"{P}.r_Speed_003", GroupName = "Decimal" });
        tt.Parameters.Add(new TrainTypeParameter { Name = "TypeOfWash", DataType = "LREAL", Value = "2", PlcVariable = $"{P}.r_TypeOfwash_012", GroupName = "Gantry" });

        if (withGantryRows)
        {
            tt.GantryData.Add(new TrainTypeGantryData { TableId = "TAB1_FW_UP", RowNumber = 1, EnableLine = true, Syncron = "Syncron", Master1xStart = 100, Slave1yStart = 10, SpeedSlaveY1Start = 1, Master1xEnd = 200, Slave1yEnd = 20, SpeedSlaveY1End = 2 });
            tt.GantryData.Add(new TrainTypeGantryData { TableId = "TAB1_FW_UP", RowNumber = 2, EnableLine = true, Syncron = "Polynom 3", Master1xStart = 200, Slave1yStart = 20, SpeedSlaveY1Start = 2, Master1xEnd = 350, Slave1yEnd = 35, SpeedSlaveY1End = 3 });
            tt.GantryData.Add(new TrainTypeGantryData { TableId = "TAB1_FW_UP", RowNumber = 3, EnableLine = false });
        }
        return tt;
    }

    [Fact]
    public async Task FullWrite_WritesEverything_AndVerifies()
    {
        var (writer, plc, _) = Create();

        var result = await writer.WriteAsync(TrainType(), Config(), new TrainTypePlcWriteOptions(), "u");

        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.Empty(result.Warnings);
        Assert.NotNull(result.Verification);
        Assert.True(result.Verification!.Success);

        // Cabecera
        Assert.Equal("Metro", plc.Memory[$"{P}.s_Name"]);
        Assert.Equal(3, plc.Memory[$"{P}.n_Line"]);
        Assert.Equal(1, plc.Memory[$"{P}.n_TableCount"]);

        // Parámetros (incluido el Gantry-config del incidente)
        Assert.Equal(true, plc.Memory[$"{P}.b_Enable_001"]);
        Assert.Equal(12.5, plc.Memory[$"{P}.r_Speed_003"]);
        Assert.Equal(2.0, plc.Memory[$"{P}.r_TypeOfwash_012"]);

        // Tabla: LineCount = líneas habilitadas
        Assert.Equal(2, plc.Memory[$"{P}.st_FwUp.n_Lines"]);

        // Modelo encadenado: L1 → puntos 1,2 ; L2 → puntos 2,3
        Assert.Equal((sbyte)1, plc.Memory[$"{P}.st_FwUp.st_Points[1].FunctionType"]);
        Assert.Equal(100.0, plc.Memory[$"{P}.st_FwUp.st_Points[1].Position_X"]);
        Assert.Equal((sbyte)3, plc.Memory[$"{P}.st_FwUp.st_Points[2].FunctionType"]);
        Assert.Equal(200.0, plc.Memory[$"{P}.st_FwUp.st_Points[2].Position_X"]);
        Assert.Equal(350.0, plc.Memory[$"{P}.st_FwUp.st_Points[3].Position_X"]);
        Assert.Equal(35.0, plc.Memory[$"{P}.st_FwUp.st_Points[3].Position_Y"]);
        Assert.Equal(3.0, plc.Memory[$"{P}.st_FwUp.st_Points[3].Speed_Y"]);
        // END de la última línea también recibe FunctionType
        Assert.Equal((sbyte)3, plc.Memory[$"{P}.st_FwUp.st_Points[3].FunctionType"]);

        // min/max height (antes no se escribían al cargar un slot)
        Assert.Equal(100.0, plc.Memory[$"{P}.st_FwUp.min_height"]);
        Assert.Equal(350.0, plc.Memory[$"{P}.st_FwUp.max_height"]);

        // Trigger al final, después de verificar
        Assert.Equal(true, plc.Memory[$"{P}.b_Write"]);
        Assert.Equal($"{P}.b_Write", plc.Writes.Last().Var);
    }

    [Fact]
    public async Task DriverRejectsWrite_IsCountedAsError_NotSuccess()
    {
        var (writer, plc, _) = Create();
        plc.RejectWrites.Add($"{P}.r_TypeOfwash_012");

        var result = await writer.WriteAsync(TrainType(), Config(), new TrainTypePlcWriteOptions(), "u");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("r_TypeOfwash_012"));
        Assert.False(plc.Memory.ContainsKey($"{P}.r_TypeOfwash_012"));
    }

    [Fact]
    public async Task PlcKeepsOldValue_VerificationDetectsMismatch_AndLogsL2()
    {
        var (writer, plc, opLog) = Create();
        // La relectura devuelve el valor "antiguo" aunque la escritura fue aceptada
        plc.ReadOverrides[$"{P}.r_TypeOfwash_012"] = 1.0;

        var result = await writer.WriteAsync(TrainType(), Config(), new TrainTypePlcWriteOptions(), "u");

        Assert.False(result.Success);
        Assert.NotNull(result.Verification);
        var mismatch = Assert.Single(result.Verification!.Mismatches);
        Assert.Contains("r_TypeOfwash_012", mismatch.Variable);
        Assert.Equal("2", mismatch.Expected);
        Assert.Equal("1", mismatch.Actual);

        opLog.Verify(o => o.LogAsync(OperationCategory.Recipe, OperationAction.PlcWriteVerifyFailed, It.IsAny<string>(),
            "u", It.IsAny<Dictionary<string, object>?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task TableConfiguredInExcel_WithoutDbRows_ProducesIntegrityWarning_AndDoesNotTouchPlc()
    {
        var (writer, plc, opLog) = Create();

        var result = await writer.WriteAsync(TrainType(withGantryRows: false), Config(), new TrainTypePlcWriteOptions(), "u");

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("TAB1_FW_UP") && w.Contains("sin datos guardados"));
        Assert.False(plc.Memory.ContainsKey($"{P}.st_FwUp.n_Lines"));
        Assert.False(plc.Memory.ContainsKey($"{P}.st_FwUp.min_height"));

        opLog.Verify(o => o.LogAsync(OperationCategory.Recipe, OperationAction.IntegrityWarning, It.IsAny<string>(),
            "u", It.IsAny<Dictionary<string, object>?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task SlotSavedWithOldSoftware_MissingGantryParam_WarnsAndWritesTheRest()
    {
        var (writer, plc, _) = Create();
        var tt = TrainType();
        tt.Parameters.Remove(tt.Parameters.Single(p => p.GroupName == "Gantry"));

        var result = await writer.WriteAsync(tt, Config(), new TrainTypePlcWriteOptions(), "u");

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("R_TYPEOFWASH_012") && w.Contains("re-guardar"));
        Assert.False(plc.Memory.ContainsKey($"{P}.r_TypeOfwash_012"));
        Assert.Equal(12.5, plc.Memory[$"{P}.r_Speed_003"]);
    }

    [Fact]
    public async Task ExcludeGantryTables_WritesOnlyParameters()
    {
        var (writer, plc, _) = Create();

        var result = await writer.WriteAsync(TrainType(), Config(), new TrainTypePlcWriteOptions { IncludeGantryTables = false }, "u");

        Assert.True(result.Success);
        Assert.Equal(2.0, plc.Memory[$"{P}.r_TypeOfwash_012"]);
        Assert.False(plc.Memory.ContainsKey($"{P}.st_FwUp.n_Lines"));
    }

    [Fact]
    public async Task AlternatePrefix_RewritesAllVariables()
    {
        var (writer, plc, _) = Create();

        var result = await writer.WriteAsync(TrainType(), Config(), new TrainTypePlcWriteOptions
        {
            IncludeGantryTables = false,
            VariableMapper = v => TrainTypePlcWriter.ApplyAlternatePrefix(v, "st_TrainRecipe2"),
            ContextLabel = "PLC ALT"
        }, "u");

        Assert.True(result.Success, string.Join(" | ", result.Errors));
        Assert.Equal(2.0, plc.Memory["MAIN.fbMachine.st_TrainRecipe2[1].r_TypeOfwash_012"]);
        Assert.Equal("Metro", plc.Memory["MAIN.fbMachine.st_TrainRecipe2[1].s_Name"]);
        Assert.False(plc.Memory.ContainsKey($"{P}.r_TypeOfwash_012"));
        Assert.All(plc.Writes, w => Assert.DoesNotContain("st_TrainRecipe[", w.Var));
    }

    [Fact]
    public async Task LineNumberOverride_IsUsed()
    {
        var (writer, plc, _) = Create();

        await writer.WriteAsync(TrainType(), Config(), new TrainTypePlcWriteOptions { LineNumberOverride = 9, IncludeGantryTables = false }, "u");

        Assert.Equal(9, plc.Memory[$"{P}.n_Line"]);
    }

    [Fact]
    public async Task NoTriggerOption_DoesNotWriteTrigger()
    {
        var (writer, plc, _) = Create();

        await writer.WriteAsync(TrainType(), Config(), new TrainTypePlcWriteOptions { WriteTrigger = false, IncludeGantryTables = false }, "u");

        Assert.False(plc.Memory.ContainsKey($"{P}.b_Write"));
    }

    [Fact]
    public async Task NullConfig_WritesPersistedParametersOnly_WithWarning()
    {
        var (writer, plc, _) = Create();

        var result = await writer.WriteAsync(TrainType(), null, new TrainTypePlcWriteOptions(), "u");

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Excel"));
        Assert.Equal(2.0, plc.Memory[$"{P}.r_TypeOfwash_012"]);
        Assert.False(plc.Memory.ContainsKey($"{P}.s_Name"));
    }

    [Theory]
    [InlineData("Syncron", 1)]
    [InlineData("Polynom 3", 3)]
    [InlineData("Polynom 5", 5)]
    [InlineData(null, 1)]
    [InlineData("otro", 1)]
    public void SyncronMapping_RoundTrip(string? syncron, int functionType)
    {
        Assert.Equal((sbyte)functionType, TrainTypePlcWriter.SyncronToFunctionType(syncron));
        if (syncron is "Syncron" or "Polynom 3" or "Polynom 5")
            Assert.Equal(syncron, TrainTypePlcWriter.FunctionTypeToSyncron(functionType));
    }

    [Fact]
    public void ApplyAlternatePrefix_ReplacesToken_OrLeavesUntouched()
    {
        Assert.Equal("MAIN.st_TrainRecipe2[1].x", TrainTypePlcWriter.ApplyAlternatePrefix("MAIN.st_TrainRecipe[1].x", "st_TrainRecipe2"));
        Assert.Equal("GVL.other", TrainTypePlcWriter.ApplyAlternatePrefix("GVL.other", "st_TrainRecipe2"));
        Assert.Equal("MAIN.st_TrainRecipe[1].x", TrainTypePlcWriter.ApplyAlternatePrefix("MAIN.st_TrainRecipe[1].x", null));
        Assert.Equal("MAIN.st_WashRecipe2.x", WashTypeParameterSynchronizer.ApplyAlternatePrefix("MAIN.st_WashRecipe.x", "st_WashRecipe2"));
    }
}

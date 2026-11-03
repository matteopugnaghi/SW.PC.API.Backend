using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Tests.Services;

/// <summary>
/// Tests de la sincronización DB ↔ PLC de parámetros de tipos de tren.
/// Cubren el incidente de campo: un parámetro Gantry (columna O/Q del Excel TrainRecipe)
/// no se persistía al guardar el slot y por tanto no se escribía al cargar el slot.
/// </summary>
public class TrainTypeParameterSynchronizerTests
{
    private const string GantryVar = "MAIN.fbMachine.st_TrainRecipe[1].r_TypeOfwash_012";
    private const string BoolVar = "MAIN.fbMachine.st_TrainRecipe[1].b_Enable_001";
    private const string DecVar = "MAIN.fbMachine.st_TrainRecipe[1].r_Speed_003";

    private static PlcTrainParameterData Plc(string group, int order, string variable, string value, string dataType = "LREAL")
        => new()
        {
            Code = TrainTypeParameterSynchronizer.BuildCode(group, order),
            Name = $"{group} {order}",
            DataType = dataType,
            Value = value,
            PlcVariable = variable,
            DisplayOrder = order,
            GroupName = group
        };

    private static TrainRecipeConfiguration Config(string? boolVar = BoolVar, string? decVar = DecVar, string? gantryVar = GantryVar)
    {
        var cfg = new TrainRecipeConfiguration();
        if (boolVar != null) cfg.BoolParameters.Add(new TrainRecipeParameter { Name = "B", PlcVariable = boolVar, DataType = "BOOL" });
        if (decVar != null) cfg.DecimalParameters.Add(new TrainRecipeParameter { Name = "D", PlcVariable = decVar, DataType = "LREAL" });
        if (gantryVar != null) cfg.GantryConfigParameters.Add(new GantryConfigParameter { Name = "G", PlcVariable = gantryVar, Visibility = "TAB1_FW_UP" });
        return cfg;
    }

    // ------------------------------------------------------------------
    // Escenario de campo: dos slots con valores distintos del parámetro Gantry
    // ------------------------------------------------------------------

    [Fact]
    public void FieldScenario_TwoSlots_KeepIndependentGantryValues()
    {
        // Slot A guardado con TypeOfwash = 1
        var slotA = new List<TrainTypeParameter>();
        TrainTypeParameterSynchronizer.Sync(slotA, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupBool, 0, BoolVar, "true", "BOOL"),
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 1, DecVar, "12.5"),
            Plc(TrainTypeParameterSynchronizer.GroupGantry, 2, GantryVar, "1"),
        });

        // Slot B guardado con TypeOfwash = 2
        var slotB = new List<TrainTypeParameter>();
        TrainTypeParameterSynchronizer.Sync(slotB, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupBool, 0, BoolVar, "false", "BOOL"),
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 1, DecVar, "7"),
            Plc(TrainTypeParameterSynchronizer.GroupGantry, 2, GantryVar, "2"),
        });

        var cfg = Config();

        var writeA = TrainTypeParameterSynchronizer.GetWritableParameters(slotA, cfg, out var warnA);
        var writeB = TrainTypeParameterSynchronizer.GetWritableParameters(slotB, cfg, out var warnB);

        Assert.Empty(warnA);
        Assert.Empty(warnB);

        var gantryA = Assert.Single(writeA, p => p.PlcVariable == GantryVar);
        var gantryB = Assert.Single(writeB, p => p.PlcVariable == GantryVar);

        Assert.Equal((1.0, typeof(double)), TrainTypeParameterSynchronizer.ToPlcValue(gantryA));
        Assert.Equal((2.0, typeof(double)), TrainTypeParameterSynchronizer.ToPlcValue(gantryB));
        Assert.Equal(TrainTypeParameterSynchronizer.GroupGantry, gantryA.GroupName);
    }

    // ------------------------------------------------------------------
    // Sync: emparejar por variable PLC, no por posición
    // ------------------------------------------------------------------

    [Fact]
    public void Sync_CreatesAllParametersOnEmptyCollection()
    {
        var existing = new List<TrainTypeParameter>();

        var result = TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupBool, 0, BoolVar, "true", "BOOL"),
            Plc(TrainTypeParameterSynchronizer.GroupGantry, 1, GantryVar, "1"),
        });

        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Empty(result.Removed);
        Assert.Equal(2, existing.Count);
        Assert.Contains(existing, p => p.PlcVariable == GantryVar && p.Value == "1" && p.ParameterCode == "GANTRY_1");
    }

    [Fact]
    public void Sync_UpdatesValueOfExistingParameterMatchedByVariable()
    {
        var entity = new TrainTypeParameter { Id = 42, ParameterCode = "GANTRY_5", PlcVariable = GantryVar, Value = "1", DataType = "LREAL" };
        var existing = new List<TrainTypeParameter> { entity };

        var result = TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupGantry, 2, GantryVar, "2"),
        });

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Removed);
        Assert.Same(entity, Assert.Single(existing));
        Assert.Equal(42, entity.Id);          // misma fila en DB
        Assert.Equal("2", entity.Value);
        Assert.Equal("GANTRY_2", entity.ParameterCode); // metadatos re-alineados con el Excel
        Assert.Equal(2, entity.DisplayOrder);
    }

    [Fact]
    public void Sync_ExcelRowShift_DoesNotMixVariables()
    {
        // DB guardada con Excel antiguo: DECIMAL_1 → r_Speed_003
        var speed = new TrainTypeParameter { ParameterCode = "DECIMAL_1", PlcVariable = DecVar, Value = "12.5", DataType = "LREAL" };
        var existing = new List<TrainTypeParameter> { speed };

        // Excel nuevo: se insertó una fila antes → r_Speed_003 pasa a ser DECIMAL_2 y DECIMAL_1 es otra variable
        const string newVar = "MAIN.fbMachine.st_TrainRecipe[1].r_Length_002";
        TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 1, newVar, "100"),
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 2, DecVar, "30"),
        });

        Assert.Equal(2, existing.Count);
        Assert.Equal("30", existing.Single(p => p.PlcVariable == DecVar).Value);
        Assert.Equal("100", existing.Single(p => p.PlcVariable == newVar).Value);
        // El emparejamiento antiguo por código habría puesto "100" en r_Speed_003
        Assert.NotEqual("100", speed.Value);
    }

    [Fact]
    public void Sync_RemovesOrphansNoLongerInExcel()
    {
        var orphan = new TrainTypeParameter { ParameterCode = "DECIMAL_9", PlcVariable = "MAIN.old.r_Removed", Value = "5", DataType = "LREAL" };
        var kept = new TrainTypeParameter { ParameterCode = "BOOL_0", PlcVariable = BoolVar, Value = "true", DataType = "BOOL" };
        var existing = new List<TrainTypeParameter> { orphan, kept };

        var result = TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupBool, 0, BoolVar, "false", "BOOL"),
        });

        Assert.Same(orphan, Assert.Single(result.Removed));
        Assert.Same(kept, Assert.Single(existing));
        Assert.Equal("false", kept.Value);
    }

    [Fact]
    public void Sync_MatchIsCaseAndWhitespaceInsensitive()
    {
        var entity = new TrainTypeParameter { ParameterCode = "GANTRY_0", PlcVariable = "  " + GantryVar.ToLowerInvariant() + " ", Value = "1" };
        var existing = new List<TrainTypeParameter> { entity };

        var result = TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupGantry, 0, GantryVar, "3"),
        });

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Added);
        Assert.Equal("3", entity.Value);
        Assert.Equal(GantryVar, entity.PlcVariable);
    }

    [Fact]
    public void Sync_IgnoresPlcParametersWithoutVariable()
    {
        var existing = new List<TrainTypeParameter>();

        var result = TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupBool, 0, "", "true", "BOOL"),
            Plc(TrainTypeParameterSynchronizer.GroupBool, 1, "   ", "true", "BOOL"),
        });

        Assert.Equal(0, result.Added);
        Assert.Empty(existing);
    }

    [Fact]
    public void Sync_DuplicateVariableInExcel_EachRowGetsItsOwnEntity()
    {
        var existing = new List<TrainTypeParameter>();

        TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 0, DecVar, "1"),
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 1, DecVar, "2"),
        });

        Assert.Equal(2, existing.Count);

        // Segunda sincronización: no debe duplicar ni borrar
        var result = TrainTypeParameterSynchronizer.Sync(existing, new[]
        {
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 0, DecVar, "3"),
            Plc(TrainTypeParameterSynchronizer.GroupDecimal, 1, DecVar, "4"),
        });

        Assert.Equal(2, result.Updated);
        Assert.Equal(0, result.Added);
        Assert.Empty(result.Removed);
        Assert.Equal(2, existing.Count);
    }

    // ------------------------------------------------------------------
    // GetWritableParameters: coherencia con el Excel actual
    // ------------------------------------------------------------------

    [Fact]
    public void GetWritable_WarnsWhenSlotLacksVariablesDefinedInExcel()
    {
        // Slot guardado con la versión anterior del software (sin parámetros Gantry)
        var parameters = new List<TrainTypeParameter>
        {
            new() { Name = "B", PlcVariable = BoolVar, Value = "true", DataType = "BOOL" },
            new() { Name = "D", PlcVariable = DecVar, Value = "1", DataType = "LREAL" },
        };

        var writable = TrainTypeParameterSynchronizer.GetWritableParameters(parameters, Config(), out var warnings);

        Assert.Equal(2, writable.Count);
        var warning = Assert.Single(warnings);
        Assert.Contains(GantryVar.ToUpperInvariant(), warning);
        Assert.Contains("re-guardar", warning);
    }

    [Fact]
    public void GetWritable_SkipsParametersNoLongerInExcel()
    {
        var parameters = new List<TrainTypeParameter>
        {
            new() { Name = "Old", PlcVariable = "MAIN.old.r_Removed", Value = "9", DataType = "LREAL" },
            new() { Name = "B", PlcVariable = BoolVar, Value = "true", DataType = "BOOL" },
        };

        var writable = TrainTypeParameterSynchronizer.GetWritableParameters(parameters, Config(decVar: null, gantryVar: null), out var warnings);

        Assert.Equal(BoolVar, Assert.Single(writable).PlcVariable);
        var warning = Assert.Single(warnings);
        Assert.Contains("MAIN.old.r_Removed", warning);
        Assert.Contains("ignorado", warning);
    }

    [Fact]
    public void GetWritable_WithoutExcelConfig_WritesEverythingWithVariable()
    {
        var parameters = new List<TrainTypeParameter>
        {
            new() { Name = "A", PlcVariable = BoolVar, Value = "true", DataType = "BOOL" },
            new() { Name = "NoVar", PlcVariable = null, Value = "1", DataType = "LREAL" },
            new() { Name = "Blank", PlcVariable = "  ", Value = "1", DataType = "LREAL" },
        };

        var writable = TrainTypeParameterSynchronizer.GetWritableParameters(parameters, null, out var warnings);

        Assert.Equal(BoolVar, Assert.Single(writable).PlcVariable);
        Assert.Empty(warnings);
    }

    [Fact]
    public void GetWritable_ExcelConfigWithoutVariables_DoesNotBlockWrite()
    {
        var parameters = new List<TrainTypeParameter>
        {
            new() { Name = "A", PlcVariable = BoolVar, Value = "true", DataType = "BOOL" },
        };

        var writable = TrainTypeParameterSynchronizer.GetWritableParameters(parameters, new TrainRecipeConfiguration(), out var warnings);

        Assert.Single(writable);
        Assert.Empty(warnings);
    }

    // ------------------------------------------------------------------
    // Conversión de valores (cultura / tipos)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", 1.5)]          // guardado por versión anterior con cultura es-ES
    [InlineData("-0.25", -0.25)]
    [InlineData(" 12.75 ", 12.75)]
    [InlineData("1E3", 1000.0)]
    [InlineData("", 0.0)]
    [InlineData(null, 0.0)]
    [InlineData("abc", 0.0)]
    public void ParseDouble_IsCultureSafe(string? text, double expected)
    {
        Assert.Equal(expected, TrainTypeParameterSynchronizer.ParseDouble(text));
    }

    [Fact]
    public void ParseDouble_NeverTreatsCommaAsThousandsSeparator()
    {
        // Con NumberStyles.Any + InvariantCulture "1,5" se parseaba como 15 → valor x10 en el PLC
        Assert.Equal(1.5, TrainTypeParameterSynchronizer.ParseDouble("1,5"));
        Assert.NotEqual(15.0, TrainTypeParameterSynchronizer.ParseDouble("1,5"));
    }

    [Theory]
    [InlineData("BOOL", "true", true)]
    [InlineData("bool", "True", true)]
    [InlineData("BOOL", "false", false)]
    [InlineData("BOOL", "garbage", false)]
    public void ToPlcValue_Bool(string dataType, string value, bool expected)
    {
        var (v, t) = TrainTypeParameterSynchronizer.ToPlcValue(new TrainTypeParameter { DataType = dataType, Value = value });
        Assert.Equal(typeof(bool), t);
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("LREAL", "2", 2.0)]
    [InlineData("REAL", "2.5", 2.5)]
    [InlineData("DOUBLE", "2,5", 2.5)]
    [InlineData("lreal ", "3", 3.0)]
    public void ToPlcValue_Double(string dataType, string value, double expected)
    {
        var (v, t) = TrainTypeParameterSynchronizer.ToPlcValue(new TrainTypeParameter { DataType = dataType, Value = value });
        Assert.Equal(typeof(double), t);
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("INT", "7", 7)]
    [InlineData("INTEGER", "7.4", 7)]
    [InlineData("DINT", "-3", -3)]
    public void ToPlcValue_Int(string dataType, string value, int expected)
    {
        var (v, t) = TrainTypeParameterSynchronizer.ToPlcValue(new TrainTypeParameter { DataType = dataType, Value = value });
        Assert.Equal(typeof(int), t);
        Assert.Equal(expected, v);
    }

    [Fact]
    public void ToPlcValue_UnknownType_FallsBackToString()
    {
        var (v, t) = TrainTypeParameterSynchronizer.ToPlcValue(new TrainTypeParameter { DataType = "STRING", Value = "Tren A" });
        Assert.Equal(typeof(string), t);
        Assert.Equal("Tren A", v);
    }

    [Fact]
    public void ToEntity_CopiesAllFieldsIncludingGroup()
    {
        var src = new PlcTrainParameterData
        {
            Code = "GANTRY_4", Name = "Tipo lavado", DataType = "LREAL", Value = "2",
            MinValue = 0, MaxValue = 9, Unit = "u", PlcVariable = " " + GantryVar + " ", DisplayOrder = 4,
            GroupName = TrainTypeParameterSynchronizer.GroupGantry
        };

        var e = TrainTypeParameterSynchronizer.ToEntity(src);

        Assert.Equal("GANTRY_4", e.ParameterCode);
        Assert.Equal("Tipo lavado", e.Name);
        Assert.Equal("LREAL", e.DataType);
        Assert.Equal("2", e.Value);
        Assert.Equal(0, e.MinValue);
        Assert.Equal(9, e.MaxValue);
        Assert.Equal("u", e.Unit);
        Assert.Equal(GantryVar, e.PlcVariable);
        Assert.Equal(4, e.DisplayOrder);
        Assert.Equal("Gantry", e.GroupName);
    }

    [Fact]
    public void GetConfiguredVariables_IncludesBoolDecimalAndGantry()
    {
        var set = TrainTypeParameterSynchronizer.GetConfiguredVariables(Config());

        Assert.Equal(3, set.Count);
        Assert.Contains(TrainTypeParameterSynchronizer.NormalizeVariable(BoolVar), set);
        Assert.Contains(TrainTypeParameterSynchronizer.NormalizeVariable(DecVar), set);
        Assert.Contains(TrainTypeParameterSynchronizer.NormalizeVariable(GantryVar), set);
    }
}

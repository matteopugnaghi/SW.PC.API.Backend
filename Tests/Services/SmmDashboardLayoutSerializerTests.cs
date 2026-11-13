using SW.PC.API.Backend.Services.Smm;

namespace SW.PC.API.Backend.Tests.Services;

public class SmmDashboardLayoutSerializerTests
{
    [Fact]
    public void Serialize_then_read_preserves_the_order()
    {
        var order = new[] { "Counters", "Washing Plant Statistics", "List of washed trains" };

        var json = SmmDashboardLayoutSerializer.Serialize(order);

        Assert.Equal(order, SmmDashboardLayoutSerializer.ReadGroupOrder(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]                          // valor por defecto de la columna
    [InlineData("{ not json")]
    [InlineData("[\"A\",\"B\"]")]               // otro formato
    [InlineData("{\"groupOrder\":\"A\"}")]
    public void Unusable_layout_json_means_default_order(string? layoutJson)
    {
        Assert.Empty(SmmDashboardLayoutSerializer.ReadGroupOrder(layoutJson));
    }

    [Fact]
    public void Normalize_drops_blanks_duplicates_and_impossible_names_keeping_first_occurrence_order()
    {
        var tooLong = new string('x', SmmDashboardLayoutSerializer.MaxGroupNameLength + 1);

        var result = SmmDashboardLayoutSerializer.NormalizeGroupOrder(
            new[] { "B", "", "A", null, "B", "   ", tooLong, "C" });

        Assert.Equal(new[] { "B", "A", "C" }, result);
    }

    [Fact]
    public void Read_ignores_non_string_entries()
    {
        var result = SmmDashboardLayoutSerializer.ReadGroupOrder("{\"groupOrder\":[\"A\",5,null,\"B\"]}");

        Assert.Equal(new[] { "A", "B" }, result);
    }
}

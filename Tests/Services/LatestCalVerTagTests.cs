using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Tests.Services;

public class LatestCalVerTagTests
{
    [Fact]
    public void Legacy_v_prefixed_tags_do_not_hide_a_newer_release()
    {
        // Orden real que devuelve `git tag --sort=-version:refname` en el repo del frontend
        var gitOutput = "v2026.09.07|2026-09-17\nv2026.09.06|2026-09-16\n2026.11.02|2026-11-14\n2026.11.01|2026-11-03\n2026.10.23|2026-10-30\n";

        var latest = SoftwareIntegrityService.PickLatestCalVerTag(gitOutput);

        Assert.Equal(("2026.11.02", "2026-11-14"), latest);
    }

    [Fact]
    public void A_v_prefixed_tag_wins_when_it_is_really_the_newest()
    {
        var latest = SoftwareIntegrityService.PickLatestCalVerTag("2026.08.03|2026-08-20\r\nv2026.09.07|2026-09-17\r\n");

        Assert.Equal(("v2026.09.07", "2026-09-17"), latest);
    }

    [Fact]
    public void Increment_is_compared_as_a_number()
    {
        var latest = SoftwareIntegrityService.PickLatestCalVerTag("2026.11.9|2026-11-20\n2026.11.10|2026-11-21\n");

        Assert.Equal("2026.11.10", latest?.Tag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n \n")]
    public void No_tags_gives_no_release(string? gitOutput)
    {
        Assert.Null(SoftwareIntegrityService.PickLatestCalVerTag(gitOutput));
    }
}

using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Domain.Tests;

public sealed class PersianDateCodeTests
{
    [Theory]
    [InlineData("2024-03-19T20:29:00Z", "14021229")]
    [InlineData("2024-03-19T20:30:00Z", "14030101")]
    [InlineData("2025-03-20T20:30:00Z", "14040101")]
    [InlineData("2026-09-11T21:00:00Z", "14050621")]
    public void OfficialDateCodeUsesPersianCalendarAndTehranCivilBoundary(string instant, string expected)
    {
        Assert.Equal(expected, PersianDateCode.FromInstant(DateTimeOffset.Parse(instant)));
    }
}

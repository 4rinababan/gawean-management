using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Tests;

public class LexoRankTests
{
    [Fact]
    public void Between_open_ends_returns_midpoint()
    {
        var rank = LexoRank.Between(null, null);

        rank.Should().NotBeNullOrEmpty();
        string.CompareOrdinal(LexoRank.Min, rank).Should().BeLessThan(0);
        string.CompareOrdinal(rank, LexoRank.Max).Should().BeLessThan(0);
    }

    [Fact]
    public void Between_two_ranks_sorts_strictly_between_them()
    {
        var a = LexoRank.Between(null, null);
        var c = LexoRank.Between(a, null);
        var b = LexoRank.Between(a, c);

        string.CompareOrdinal(a, b).Should().BeLessThan(0);
        string.CompareOrdinal(b, c).Should().BeLessThan(0);
    }

    [Fact]
    public void Between_adjacent_digits_descends_a_level_without_collision()
    {
        var lower = LexoRank.Between("i", "j");

        string.CompareOrdinal("i", lower).Should().BeLessThan(0);
        string.CompareOrdinal(lower, "j").Should().BeLessThan(0);
    }

    [Fact]
    public void Repeated_insertion_at_the_front_keeps_order_stable()
    {
        var ranks = new List<string> { LexoRank.Between(null, null) };
        for (var i = 0; i < 50; i++)
            ranks.Insert(0, LexoRank.Between(null, ranks[0]));

        ranks.Should().BeInAscendingOrder(StringComparer.Ordinal);
        ranks.Distinct().Should().HaveCount(ranks.Count);
    }

    [Fact]
    public void Initial_produces_ascending_distinct_ranks()
    {
        var ranks = LexoRank.Initial(10);

        ranks.Should().HaveCount(10);
        ranks.Should().BeInAscendingOrder(StringComparer.Ordinal);
        ranks.Distinct().Should().HaveCount(10);
    }

    [Fact]
    public void Between_throws_when_bounds_are_out_of_order()
    {
        var act = () => LexoRank.Between("q", "b");

        act.Should().Throw<DomainException>();
    }
}

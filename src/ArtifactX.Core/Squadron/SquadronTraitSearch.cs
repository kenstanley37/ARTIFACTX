namespace ArtifactX.Core.Squadron;

/// <summary>A found traits seed and its stat card; <see cref="IsBest"/> when every trait is at the best the rank allows.</summary>
public sealed record TraitSearchResult(ulong Seed, PilotFacts Facts, bool IsBest, long Tried);

/// <summary>
/// Looks for the traits seed with the best stat card a pilot of a given rank can have (Squadron page, "Find best
/// traits"). The rank caps the two flying skills and the good trait (SquadronPilotFacts: the rank's grade range): S
/// reaches Unrivaled, A Outstanding, B Excellent, C Good; the rogue trait reaches Unrivaled at any rank. Lower grade
/// numbers are better (1 Unrivaled ... 10 Non-existent), so a card scores the sum of its four grades, more kills
/// breaking ties. An all-best S card is about one seed in two million (each capped trait needs its four draws to
/// average 0.8 or more, about 1.7%; the rogue trait 1 in 10).
/// </summary>
public static class SquadronTraitSearch
{
    /// <summary>The best grade the rank's range allows for a capped trait: the grade formula with the four draws
    /// averaging 0.999. Higher averages can't be found in practice (four draws all over 0.999 is about one in 10^12),
    /// and nearer 1 the float maths would round a class A trait up to Unrivaled, which no real seed reaches.</summary>
    public static int BestGrade(int rank, IReadOnlyList<(float Low, float High)> traitRanges)
    {
        int r = Math.Clamp(rank, 0, 3);
        var (low, high) = r < traitRanges.Count ? traitRanges[r] : (0f, 1f);
        const float HighestMean = 0.999f;
        int grade = 10 - (int)((HighestMean * (high - low) + low) * 10f);
        return grade < 2 ? 1 : Math.Min(grade, 10);
    }

    /// <summary>Tries random seeds on every core until an all-best card turns up, the time runs out or it's cancelled,
    /// and returns the best card seen.</summary>
    public static TraitSearchResult Find(int rank, IReadOnlyList<(float Low, float High)> traitRanges, TimeSpan timeLimit,
        CancellationToken cancel = default)
    {
        int best = BestGrade(rank, traitRanges);
        int target = best * 3 + 1;
        var deadline = DateTime.UtcNow + timeLimit;
        object gate = new();
        (ulong Seed, PilotFacts Facts, int Score)? top = null;
        long tried = 0;
        int topScore = int.MaxValue;
        using var done = CancellationTokenSource.CreateLinkedTokenSource(cancel);

        Parallel.For(0, Environment.ProcessorCount, new ParallelOptions { CancellationToken = CancellationToken.None }, _ =>
        {
            Span<byte> bytes = stackalloc byte[8];
            int count = 0;
            while (!done.IsCancellationRequested)
            {
                Random.Shared.NextBytes(bytes);
                ulong seed = BitConverter.ToUInt64(bytes);
                var facts = SquadronPilotFacts.Generate(seed, rank, traitRanges);
                int score = facts.Skills.Sum(s => s.Grade);
                if (score <= Volatile.Read(ref topScore))
                    lock (gate)
                        if (top is not { } t || score < t.Score || (score == t.Score && facts.Kills > t.Facts.Kills))
                        {
                            top = (seed, facts, score);
                            Volatile.Write(ref topScore, score);
                            if (score <= target) done.Cancel();
                        }
                if (++count % 4096 == 0)
                {
                    Interlocked.Add(ref tried, 4096);
                    if (DateTime.UtcNow > deadline) done.Cancel();
                }
            }
        });

        var (foundSeed, foundFacts, foundScore) = top!.Value;
        return new TraitSearchResult(foundSeed, foundFacts, foundScore <= target, tried);
    }
}

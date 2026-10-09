using System;

namespace Synapsis.Api.Tests.Support;

/// <summary>A clock that always says the same time, so receipts and known_at times are predictable.</summary>
public sealed class FixedClock : TimeProvider
{
    /// <summary>The time it says: 9 October 2026, 08:00:00.250 UTC.</summary>
    public static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 9, 8, 0, 0, 250, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}

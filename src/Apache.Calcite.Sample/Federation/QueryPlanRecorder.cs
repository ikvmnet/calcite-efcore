using System.Collections.Concurrent;

namespace Apache.Calcite.Sample.Federation;

/// <summary>
/// Keeps the most recent plans Calcite produced, so the sample can show what a REST or GraphQL request actually
/// turned into. Every federated connection registers hooks that feed this recorder.
/// </summary>
public sealed class QueryPlanRecorder
{

    /// <summary>
    /// One captured stage of one query.
    /// </summary>
    /// <param name="Sequence">The monotonic position of this entry in the capture stream.</param>
    /// <param name="CapturedAt">The instant the stage was captured.</param>
    /// <param name="Stage">The Calcite hook the entry came from.</param>
    /// <param name="Payload">The rendered plan.</param>
    public sealed record Entry(long Sequence, DateTimeOffset CapturedAt, string Stage, string Payload);

    /// <summary>
    /// The number of entries retained before the oldest are dropped.
    /// </summary>
    public const int Capacity = 200;

    readonly ConcurrentQueue<Entry> _entries = new();
    long _sequence;

    /// <summary>
    /// Records one plan stage.
    /// </summary>
    /// <param name="stage">The Calcite hook the entry came from.</param>
    /// <param name="payload">The rendered plan.</param>
    public void Record(string stage, string payload)
    {
        _entries.Enqueue(new Entry(Interlocked.Increment(ref _sequence), DateTimeOffset.UtcNow, stage, payload));

        while (_entries.Count > Capacity && _entries.TryDequeue(out _))
            ;
    }

    /// <summary>
    /// Gets the retained entries, newest first.
    /// </summary>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <returns>The retained entries.</returns>
    public IReadOnlyList<Entry> Recent(int count = 25)
    {
        return _entries.Reverse().Take(count).ToList();
    }

    /// <summary>
    /// Discards every retained entry.
    /// </summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _))
            ;
    }

}

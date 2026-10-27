using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ArtifactX.Core.Collections;

// No desktop dependencies: run with dotnet run --project tests/ArtifactX.Core.Checks.
try
{
    var rows = new ObservableCollection<Row> { new(1, "A", 10), new(2, "B", 20) };
    var first = rows[0];
    var second = rows[1];
    int changes = 0, resets = 0;
    rows.CollectionChanged += (_, e) =>
    {
        changes++;
        if (e.Action == NotifyCollectionChangedAction.Reset) resets++;
    };
    void Apply(params Row[] incoming) => ObservableCollectionSync.Apply(rows, incoming,
        row => row.Id, (a, b) => a.Model == b.Model, (a, b) => a.Value = b.Value);

    for (int i = 0; i < 100; i++) Apply(new(1, "A", 10), new(2, "B", 20));
    Require(changes == 0 && ReferenceEquals(first, rows[0]) && ReferenceEquals(second, rows[1]),
        "Unchanged snapshots recreated rows");

    Apply(new(1, "A", 11), new(2, "B", 20));
    Require(changes == 0 && rows[0].Value == 11 && ReferenceEquals(first, rows[0]),
        "A stats update replaced the model row");

    Apply(new(2, "B", 20), new(1, "A", 11));
    Require(changes == 1 && ReferenceEquals(first, rows[1]) && ReferenceEquals(second, rows[0]),
        "Reordering lost row identity");

    Apply(new(2, "C", 30), new(1, "A", 11));
    Require(!ReferenceEquals(second, rows[0]) && ReferenceEquals(first, rows[1]),
        "Model replacement affected an unrelated row");

    int before = changes;
    bool rejected = false;
    try { Apply(new(1, "A", 1), new(1, "B", 2)); }
    catch (ArgumentException) { rejected = true; }
    Require(rejected && changes == before, "Duplicate keys were accepted or partially published");

    var random = new Random(1027);
    for (int i = 0; i < 1000; i++)
    {
        var incoming = Enumerable.Range(0, 10).OrderBy(_ => random.Next()).Take(random.Next(11))
            .Select(id => new Row(id, "same", i)).ToArray();
        var retained = rows.ToDictionary(row => row.Id);
        Apply(incoming);
        Require(rows.Select(row => (row.Id, row.Model, row.Value))
            .SequenceEqual(incoming.Select(row => (row.Id, row.Model, row.Value))),
            "Mixed-update output differs from incoming data");
        foreach (var row in rows)
            if (retained.TryGetValue(row.Id, out var old) && old.Model == row.Model)
                Require(ReferenceEquals(old, row), "A surviving row was replaced");
    }
    Require(resets == 0, "A collection Reset was emitted");
    Console.WriteLine("PASS: unchanged snapshots, in-place stats, reorder, model replacement, duplicate rejection, and 1000 mixed updates; zero resets.");
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class Row(int id, string model, int value)
{
    public int Id { get; } = id;
    public string Model { get; } = model;
    public int Value { get; set; } = value;
}

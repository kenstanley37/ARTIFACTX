using System.Collections.ObjectModel;

namespace ArtifactX.Core.Collections;

/// <summary>Reconciles keyed display rows without resetting unchanged objects or their collection.</summary>
public static class ObservableCollectionSync
{
    public static void Apply<T, TKey>(ObservableCollection<T> target, IReadOnlyList<T> incoming,
        Func<T, TKey> key, Func<T, T, bool> equivalent, Action<T, T>? update = null) where TKey : notnull
    {
        // Validate before publishing any changes. Duplicate keys cannot identify a stable row.
        if (incoming.Select(key).Distinct().Count() != incoming.Count
            || target.Select(key).Distinct().Count() != target.Count)
            throw new ArgumentException("Display row keys must be unique.");
        var comparer = EqualityComparer<TKey>.Default;
        for (int index = 0; index < incoming.Count; index++)
        {
            var next = incoming[index];
            int found = index;
            while (found < target.Count && !comparer.Equals(key(target[found]), key(next))) found++;
            if (found == target.Count) target.Insert(index, next);
            else
            {
                if (found != index) target.Move(found, index);
                if (equivalent(target[index], next)) update?.Invoke(target[index], next);
                else target[index] = next;
            }
        }
        while (target.Count > incoming.Count) target.RemoveAt(target.Count - 1);
    }
}

using Lumina.Excel.Sheets;

namespace Umbra.TimedNodes;

public enum GatherJob
{
    Miner,
    Botanist,
}

/// <summary>Une fenêtre d'apparition, en minutes d'Eorzéa depuis minuit.</summary>
public readonly record struct SpawnWindow(int StartMinute, int DurationMinutes);

public sealed record TimedNode(
    uint              BaseId,
    GatherJob         Job,
    int               Level,
    bool              IsEphemeral,
    string            ItemName,
    string[]          AllItemNames,
    uint              IconId,
    string            ZoneName,
    uint              TerritoryId,
    uint              MapId,
    Vector2           MapCoords,
    Vector3           WorldPosition,
    List<SpawnWindow> Windows,
    uint              FallbackAetheryteId
);

/// <summary>État calculé d'un nœud à un instant donné.</summary>
public readonly record struct NodeStatus(TimedNode Node, bool IsActive, double RealSeconds)
{
    /// <summary>Secondes réelles restantes (si actif) ou avant apparition (sinon).</summary>
    public double RealSeconds { get; } = RealSeconds;
}

public static class EorzeaClock
{
    /// <summary>Une minute d'Eorzéa = 175/60 secondes réelles.</summary>
    public const double RealSecondsPerEorzeaMinute = 175.0 / 60.0;

    /// <summary>Minute d'Eorzéa courante dans la journée (0 à 1439, avec décimales).</summary>
    public static double CurrentMinuteOfDay()
    {
        double realSeconds    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        double eorzeaMinutes  = realSeconds / RealSecondsPerEorzeaMinute;
        return eorzeaMinutes % 1440.0;
    }

    public static string FormatRealDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(seconds)));
        return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}" : $"{ts.Minutes}:{ts.Seconds:00}";
    }
}

public static class TimedNodeRepository
{
    private static List<TimedNode>? _nodes;

    public static IReadOnlyList<TimedNode> Nodes => _nodes ??= Load();

    public static void Reset() => _nodes = null;

    public static NodeStatus GetStatus(TimedNode node, double nowMinute)
    {
        double bestWait = double.MaxValue;

        foreach (var w in node.Windows) {
            double sinceStart = Mod(nowMinute - w.StartMinute, 1440);

            if (sinceStart < w.DurationMinutes) {
                return new(node, true, (w.DurationMinutes - sinceStart) * EorzeaClock.RealSecondsPerEorzeaMinute);
            }

            double wait = Mod(w.StartMinute - nowMinute, 1440);
            if (wait < bestWait) bestWait = wait;
        }

        return new(node, false, bestWait * EorzeaClock.RealSecondsPerEorzeaMinute);
    }

    private static double Mod(double a, double m) => ((a % m) + m) % m;

    /// <summary>Convertit une valeur HHMM du jeu en minutes (ex. 1230 → 750, 160 → 120).</summary>
    private static int HhmmToMinutes(int hhmm) => hhmm / 100 * 60 + hhmm % 100;

    private static List<TimedNode> Load()
    {
        var nodes = BuildFrom(Framework.Service<IDataManager>().Excel);
        Logger.Info($"[TimedNodes] {nodes.Count} nœuds temporaires chargés.");
        return nodes;
    }

    /// <summary>Construit la liste des nœuds temporaires à partir des feuilles Excel du jeu.</summary>
    public static List<TimedNode> BuildFrom(Lumina.Excel.ExcelModule excel)
    {
        var points     = excel.GetSheet<GatheringPoint>();
        var transients = excel.GetSheet<GatheringPointTransient>();
        var exported   = excel.GetSheet<ExportedGatheringPoint>();

        Dictionary<uint, TimedNode> result = [];

        foreach (var point in points) {
            if (!transients.TryGetRow(point.RowId, out var transient)) continue;

            List<SpawnWindow> windows   = [];
            bool              ephemeral = false;

            // Nœuds limités / légendaires : table d'horaires.
            if (transient.GatheringRarePopTimeTable.RowId != 0
                && transient.GatheringRarePopTimeTable.TryGetValue(out var table)) {
                for (int i = 0; i < table.StartTime.Count; i++) {
                    int start = table.StartTime[i];
                    if (start == ushort.MaxValue) continue;

                    int duration = HhmmToMinutes(table.Duration[i]);
                    if (duration == 0) {
                        if (start == 0 && i > 0) continue; // emplacement vide
                        duration = 120;                    // par défaut : 2 h d'Eorzéa
                    }

                    windows.Add(new(HhmmToMinutes(start), duration));
                }
            }

            // Nœuds éphémères (à aethérologie).
            if (windows.Count == 0
                && transient.EphemeralStartTime != ushort.MaxValue
                && transient.EphemeralEndTime   != ushort.MaxValue
                && transient.EphemeralStartTime != transient.EphemeralEndTime) {
                int start = HhmmToMinutes(transient.EphemeralStartTime);
                int end   = HhmmToMinutes(transient.EphemeralEndTime);
                windows.Add(new(start, (int)((end - start + 1440) % 1440)));
                ephemeral = true;
            }

            if (windows.Count == 0) continue;
            if (!point.GatheringPointBase.TryGetValue(out var pointBase)) continue;
            if (result.ContainsKey(pointBase.RowId)) continue;

            GatherJob? job = pointBase.GatheringType.RowId switch {
                0 or 1 => GatherJob.Miner,
                2 or 3 => GatherJob.Botanist,
                _      => null,
            };
            if (job == null) continue;

            if (!point.TerritoryType.TryGetValue(out var territory) || territory.RowId == 0) continue;

            // Objets récoltables du nœud.
            List<(string Name, uint Icon, uint Id)> items = [];
            foreach (var itemRef in pointBase.Item) {
                if (itemRef.RowId == 0) continue;
                if (!itemRef.TryGetValue<GatheringItem>(out var gatheringItem)) continue;
                if (!gatheringItem.Item.TryGetValue<Item>(out var item)) continue;

                string name = item.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name)) continue;

                items.Add((name, item.Icon, item.RowId));
            }
            if (items.Count == 0) continue;

            // On met en avant le premier objet qui n'est pas un cristal (IDs 2 à 19).
            var main = items.FirstOrDefault(i => i.Id >= 20);
            if (main.Name == null) main = items[0];

            string zoneName = territory.PlaceName.TryGetValue(out var place) ? place.Name.ExtractText() : "";
            if (string.IsNullOrWhiteSpace(zoneName)) continue; // nœuds inutilisés (territoire factice)
            if (point.PlaceName.TryGetValue(out var subPlace)) {
                string sub = subPlace.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(sub) && sub != zoneName) zoneName = $"{zoneName} – {sub}";
            }

            Vector3 world  = Vector3.Zero;
            Vector2 coords = Vector2.Zero;
            uint    mapId  = territory.Map.RowId;

            if (exported.TryGetRow(pointBase.RowId, out var exp)) {
                world = new(exp.X, 0, exp.Y);

                if (territory.Map.TryGetValue(out var map)) {
                    coords = new(
                        WorldToMap(exp.X, map.SizeFactor, map.OffsetX),
                        WorldToMap(exp.Y, map.SizeFactor, map.OffsetY)
                    );
                }
            }

            result[pointBase.RowId] = new(
                pointBase.RowId,
                job.Value,
                pointBase.GatheringLevel,
                ephemeral,
                main.Name,
                items.Select(i => i.Name).Distinct().ToArray(),
                main.Icon,
                zoneName,
                territory.RowId,
                mapId,
                coords,
                world,
                windows,
                territory.Aetheryte.RowId
            );
        }

        return result.Values.ToList();
    }

    private static float WorldToMap(float value, ushort sizeFactor, short offset)
    {
        float scale = sizeFactor / 100f;
        return (41f / scale) * ((value + offset) * scale + 1024f) / 2048f + 1f;
    }
}

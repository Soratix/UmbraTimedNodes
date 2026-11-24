using System.Reflection;
using Dalamud.Game.Text;
using Umbra.Windows;
using Umbra.Windows.Components;
using Umbra.Windows.Library.VariableEditor;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Umbra.TimedNodes;

/// <summary>
/// Le nom et la description d'un [ToolbarWidget] sont des constantes. Umbra lit l'attribut
/// avec GetCustomAttribute au démarrage de son framework, ce qui accepte une sous-classe :
/// celle-ci choisit le nom et la description dans la langue du jeu à ce moment-là.
/// </summary>
internal sealed class LocalizedToolbarWidgetAttribute() : ToolbarWidgetAttribute(
    "TimedNodesTracker",
    Strings.T("WidgetName"),
    Strings.T("WidgetDesc"),
    ["gathering", "node", "timed", "unspoiled", "legendary", "ephemeral", "récolte", "noeud", "minage", "botanique", "miner", "botanist"]
);

[LocalizedToolbarWidget]
public sealed unsafe class TimedNodesWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.SubText |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.ProgressBar;

    protected override string DefaultIconType   => IconTypeGameIcon;
    protected override uint   DefaultGameIconId => 60438; // icône générique de récolte

    public override MenuPopup Popup { get; } = new();

    private readonly HashSet<uint>                        _activeLastTick = [];
    private readonly Dictionary<uint, MenuPopup.Button>   _buttons        = [];
    private          List<NodeStatus> _statuses    = [];
    private          string           _menuSignature = string.Empty;
    private          DateTime         _nextRefresh   = DateTime.MinValue;
    private          bool             _firstTick     = true;

    // ------------------------------------------------------------------ Config

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        string cat = Strings.T("WidgetName");

        return [
            ..base.GetConfigVariables(),
            new SelectWidgetConfigVariable(
                "TrackMode", Strings.T("Cfg.TrackMode"), Strings.T("Cfg.TrackMode.Desc"),
                "All",
                new() { ["Tracked"] = Strings.T("Cfg.TrackMode.Tracked"), ["All"] = Strings.T("Cfg.TrackMode.All") }
            ) { Category = cat },
            new SelectWidgetConfigVariable(
                "JobFilter", Strings.T("Cfg.Job"), Strings.T("Cfg.Job.Desc"), "All",
                new() { ["All"] = Strings.T("Cfg.Job.All"), ["Miner"] = Strings.T("Miner"), ["Botanist"] = Strings.T("Botanist") }
            ) { Category = cat },
            new IntegerWidgetConfigVariable("MinLevel", Strings.T("Cfg.MinLevel"), null, 1, 1, 100) { Category = cat },
            new IntegerWidgetConfigVariable("MaxLevel", Strings.T("Cfg.MaxLevel"), null, 100, 1, 100) { Category = cat },
            new BooleanWidgetConfigVariable(
                "ShowEphemeral", Strings.T("Cfg.Ephemeral"), Strings.T("Cfg.Ephemeral.Desc"), true
            ) { Category = cat },
            new StringWidgetConfigVariable(
                "NameFilter", Strings.T("Cfg.NameFilter"), Strings.T("Cfg.NameFilter.Desc"),
                "", 512
            ) { Category = cat },
            new IntegerWidgetConfigVariable("MaxEntries", Strings.T("Cfg.MaxEntries"), null, 15, 1, 60) { Category = cat },
            new SelectWidgetConfigVariable(
                "ClickAction", Strings.T("Cfg.ClickAction"), Strings.T("Cfg.ClickAction.Desc"), "TeleportFlag",
                new() {
                    ["TeleportFlag"] = Strings.T("Cfg.ClickAction.TeleportFlag"),
                    ["Teleport"]     = Strings.T("Cfg.ClickAction.Teleport"),
                    ["Map"]          = Strings.T("Cfg.ClickAction.Map"),
                }
            ) { Category = cat },
            new BooleanWidgetConfigVariable(
                "ChatAlert", Strings.T("Cfg.ChatAlert"), Strings.T("Cfg.ChatAlert.Desc"), false
            ) { Category = cat },
            ..GetTrackingVariables(),
        ];
    }

    /// <summary>Une case à cocher par nœud, groupées par métier et tranche de niveau.</summary>
    private static IEnumerable<IWidgetConfigVariable> GetTrackingVariables()
    {
        IReadOnlyList<TimedNode> nodes;
        try {
            nodes = TimedNodeRepository.Nodes;
        } catch (Exception e) {
            Logger.Error($"[TimedNodes] Impossible de construire la liste de suivi : {e.Message}");
            yield break;
        }

        // Tranches de niveau de la plus haute (dernière extension) à la plus basse, mineur puis botaniste dans chaque tranche.
        var ordered = nodes
            .OrderByDescending(n => LevelBucket(n.Level))
            .ThenBy(n => n.Job)
            .ThenByDescending(n => n.Level)
            .ThenBy(n => n.ItemName, StringComparer.CurrentCultureIgnoreCase);

        foreach (var node in ordered) {
            string group = $"{JobTitle(node.Job)} · {LevelBucketLabel(node.Level)}";

            yield return new BooleanWidgetConfigVariable(
                TrackId(node),
                Strings.T("Cfg.Node", node.ItemName, node.Level, node.IsEphemeral ? Strings.T("Cfg.Node.Ephemeral") : "", node.ZoneName),
                Strings.T("Cfg.Node.Desc", string.Join(", ", node.AllItemNames), FormatWindows(node)),
                false
            ) { Category = Strings.T("Cfg.TrackedNodes"), Group = group };
        }
    }

    // ------------------------------------------------- Groupes repliés par défaut

    private static readonly PropertyInfo? WindowRootNodeProperty =
        typeof(Window).GetProperty("RootNode", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private Window? _pendingEditor;
    private int     _pendingFrames;

    private void OnWindowOpened(IWindow window)
    {
        if (window is not VariablesEditorWindow editor) return;

        // La fenêtre construit son contenu à sa première image : on la surveille quelques images.
        _pendingEditor = editor;
        _pendingFrames = 120;
    }

    /// <summary>
    /// Umbra ouvre toujours les groupes repliables des réglages. On replie nos groupes
    /// « Mineur · niv. … » / « Botaniste · niv. … » pour qu'on ne les ouvre qu'au besoin.
    /// </summary>
    private void CollapseTrackingGroups()
    {
        if (_pendingEditor == null) return;

        if (--_pendingFrames < 0 || _pendingEditor.IsClosed) {
            _pendingEditor = null;
            return;
        }

        if (WindowRootNodeProperty?.GetValue(_pendingEditor) is not Node root) return;

        var labels = TrackingGroupLabels();
        int count  = 0;

        foreach (var group in FindGroups(root)) {
            if (!labels.Contains(group.Label ?? string.Empty)) continue;

            group.ToggleClass("collapsed", true);
            count++;
        }

        if (count > 0) _pendingEditor = null;
    }

    private static IEnumerable<CollapsibleGroupNode> FindGroups(Node node)
    {
        foreach (var child in node.ChildNodes) {
            if (child is CollapsibleGroupNode group) yield return group;

            foreach (var nested in FindGroups(child)) yield return nested;
        }
    }

    // Pas de cache : les libellés dépendent de la langue courante.
    private static HashSet<string> TrackingGroupLabels() =>
        TimedNodeRepository.Nodes
            .Select(n => $"{JobTitle(n.Job)} · {LevelBucketLabel(n.Level)}")
            .ToHashSet();

    /// <summary>Tranche de niveau : 0 pour ≤ 50, puis 1 pour 51-60, 2 pour 61-70, etc.</summary>
    private static int LevelBucket(int level) => level <= 50 ? 0 : (level - 51) / 10 + 1;

    private static string LevelBucketLabel(int level)
    {
        int bucket = LevelBucket(level);
        if (bucket == 0) return Strings.T("Level", 50);

        int from = 51 + (bucket - 1) * 10;
        return Strings.T("LevelRange", from, from + 9);
    }

    private static string TrackId(TimedNode node) => $"Track_{node.BaseId}";

    private static string JobTitle(GatherJob job) => Strings.T(job == GatherJob.Miner ? "Miner" : "Botanist");

    // --------------------------------------------------------------- Lifecycle

    protected override void OnLoad()
    {
        Popup.OnPopupOpen += OnPopupOpened;
        Framework.Service<WindowManager>().OnWindowOpened += OnWindowOpened;
        Node.OnRightClick += _ => ActivateNode(_statuses.FirstOrDefault().Node);

        SetText(Strings.T("WidgetName"));
        SetSubText(Strings.T("Loading"));
    }

    protected override void OnUnload()
    {
        Popup.OnPopupOpen -= OnPopupOpened;
        Framework.Service<WindowManager>().OnWindowOpened -= OnWindowOpened;
        _pendingEditor = null;
        Popup.Clear(true);
        _buttons.Clear();
    }

    protected override void OnDraw()
    {
        CollapseTrackingGroups();

        if (DateTime.UtcNow < _nextRefresh) return;
        _nextRefresh = DateTime.UtcNow.AddMilliseconds(500);

        IReadOnlyList<TimedNode> nodes;
        try {
            nodes = TimedNodeRepository.Nodes;
        } catch (Exception e) {
            Logger.Error($"[TimedNodes] Impossible de lire les données du jeu : {e}");
            SetText(Strings.T("WidgetName"));
            SetSubText(Strings.T("LoadError"));
            return;
        }

        double now = EorzeaClock.CurrentMinuteOfDay();

        _statuses = nodes
            .Where(PassesFilters)
            .Select(n => TimedNodeRepository.GetStatus(n, now))
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.RealSeconds)
            .ToList();

        HandleAlerts();
        UpdateToolbarButton();

        if (Popup.IsOpen) UpdateMenu();
    }

    // ----------------------------------------------------------------- Display

    private void UpdateToolbarButton()
    {
        if (_statuses.Count == 0) {
            SetText(Strings.T("WidgetName"));
            SetSubText(GetConfigValue<string>("TrackMode") == "Tracked"
                ? Strings.T("NoTrackedNode")
                : Strings.T("NoMatch"));
            ProgressBarVisibility = false;
            SetTooltip(null);
            return;
        }

        var top  = _statuses[0];
        var node = top.Node;

        SetGameIconId(node.IconId);
        SetText(node.ItemName);
        SetSubText(top.IsActive
            ? Strings.T("SubActive", node.ZoneName, EorzeaClock.FormatRealDuration(top.RealSeconds))
            : Strings.T("SubUpcoming", node.ZoneName, EorzeaClock.FormatRealDuration(top.RealSeconds)));

        // Barre de progression : temps restant d'un nœud actif.
        ProgressBarVisibility = top.IsActive;
        if (top.IsActive) {
            int total = (int)(node.Windows.Max(w => w.DurationMinutes) * EorzeaClock.RealSecondsPerEorzeaMinute);
            SetProgressBarConstraint(0, Math.Max(1, total));
            SetProgressBarValue((int)top.RealSeconds);
        }

        SetTooltip(Strings.T(
            "Tooltip",
            node.ItemName, node.Level, JobName(node.Job),
            node.ZoneName, node.MapCoords.X, node.MapCoords.Y,
            string.Join(", ", node.AllItemNames),
            FormatWindows(node),
            ClickActionLabel()
        ));
    }

    private void OnPopupOpened()
    {
        _menuSignature = string.Empty;
        UpdateMenu();
    }

    private void UpdateMenu()
    {
        int max     = GetConfigValue<int>("MaxEntries");
        var entries = _statuses.Take(max).ToList();

        string signature = string.Join(",", entries.Select(e => e.Node.BaseId));

        if (signature != _menuSignature) {
            _menuSignature = signature;
            Popup.Clear(true);
            _buttons.Clear();

            if (entries.Count == 0) {
                Popup.Add(new MenuPopup.Header(Strings.T("NoMatch")));
                return;
            }

            int index = 0;
            foreach (var entry in entries) {
                var node = entry.Node;
                var button = new MenuPopup.Button($"{node.ItemName}  ·  {node.ZoneName}") {
                    Id        = $"Node_{node.BaseId}",
                    SortIndex = index++,
                    Icon      = node.IconId,
                    OnClick   = () => ActivateNode(node),
                };
                _buttons[node.BaseId] = button;
                Popup.Add(button);
            }
        }

        // Mise à jour des comptes à rebours sans reconstruire le menu.
        foreach (var entry in entries) {
            if (!_buttons.TryGetValue(entry.Node.BaseId, out var button)) continue;

            button.AltText = entry.IsActive
                ? Strings.T("AltActive", EorzeaClock.FormatRealDuration(entry.RealSeconds))
                : Strings.T("AltUpcoming", EorzeaClock.FormatRealDuration(entry.RealSeconds));
            button.AltTextColor = entry.IsActive ? new Color(0xFF66DD66) : null;
        }
    }

    // ----------------------------------------------------------------- Helpers

    private bool PassesFilters(TimedNode node)
    {
        if (GetConfigValue<string>("TrackMode") == "Tracked") {
            return HasConfigVariable(TrackId(node)) && GetConfigValue<bool>(TrackId(node));
        }

        string job = GetConfigValue<string>("JobFilter");
        if (job == "Miner" && node.Job != GatherJob.Miner) return false;
        if (job == "Botanist" && node.Job != GatherJob.Botanist) return false;

        if (node.Level < GetConfigValue<int>("MinLevel") || node.Level > GetConfigValue<int>("MaxLevel")) return false;
        if (node.IsEphemeral && !GetConfigValue<bool>("ShowEphemeral")) return false;

        string filter = GetConfigValue<string>("NameFilter");
        if (string.IsNullOrWhiteSpace(filter)) return true;

        var keywords = filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return keywords.Any(k =>
            node.ZoneName.Contains(k, StringComparison.OrdinalIgnoreCase)
            || node.AllItemNames.Any(i => i.Contains(k, StringComparison.OrdinalIgnoreCase)));
    }

    private void HandleAlerts()
    {
        var activeNow = _statuses.Where(s => s.IsActive).Select(s => s.Node).ToList();

        if (!_firstTick && GetConfigValue<bool>("ChatAlert")) {
            foreach (var node in activeNow.Where(n => !_activeLastTick.Contains(n.BaseId))) {
                Framework.Service<IChatGui>().Print(
                    Strings.T("ChatSpawned", node.ItemName, node.ZoneName, node.MapCoords.X, node.MapCoords.Y, node.Level, JobName(node.Job)),
                    Strings.T("ChatSource")
                );
            }
        }

        _activeLastTick.Clear();
        foreach (var n in activeNow) _activeLastTick.Add(n.BaseId);
        _firstTick = false;
    }

    private string ClickActionLabel() => GetConfigValue<string>("ClickAction") switch {
        "Map" => Strings.T("Hint.Map"),
        _     => Strings.T("Hint.Teleport"),
    };

    private void ActivateNode(TimedNode? node)
    {
        if (node == null) return;

        string action = GetConfigValue<string>("ClickAction");

        if (action == "Map") {
            OpenMap(node);
            return;
        }

        Framework.DalamudFramework.Run(() => {
            // OpenMapWithMapLink place le drapeau ET ouvre la carte (même méthode que l'ancienne version).
            if (action == "TeleportFlag" && node.MapId != 0) {
                Framework.Service<IGameGui>().OpenMapWithMapLink(node.TerritoryId, node.MapId, node.WorldPosition);
            }

            TeleportToNearestAetheryte(node);
        });
    }

    private static void TeleportToNearestAetheryte(TimedNode node)
    {
        var toast  = Framework.Service<IToastGui>();
        var player = Framework.Service<IPlayer>();

        if (!player.CanUseTeleportAction) {
            toast.ShowError(Strings.T("CannotTeleport"));
            return;
        }

        IZone zone;
        try {
            zone = Framework.Service<IZoneManager>().GetZone(node.MapId);
        } catch (Exception e) {
            Logger.Error($"[TimedNodes] Zone introuvable pour la carte {node.MapId} : {e.Message}");
            toast.ShowError(Strings.T("ZoneNotFound"));
            return;
        }

        // Aethérites de la zone que le joueur a déjà harmonisées.
        var unlocked = Framework.Service<IAetheryteList>().Where(a => a.SubIndex == 0).ToList();
        var target   = new Vector2(node.WorldPosition.X, node.WorldPosition.Z);

        var best = zone.StaticMarkers
            .Where(m => m.Type == ZoneMarkerType.Aetheryte)
            .Select(m => (Marker: m, Entry: unlocked.FirstOrDefault(a => a.AetheryteId == m.DataId)))
            .Where(x => x.Entry != null)
            .OrderBy(x => Vector2.Distance(new(x.Marker.WorldPosition.X, x.Marker.WorldPosition.Z), target))
            .FirstOrDefault();

        // Pas d'aethérite sur la carte (ex. Arrière-pays dravanien) : aethérite de repli du territoire (Idyllée).
        if (best.Entry == null && node.FallbackAetheryteId != 0) {
            best = (default, unlocked.FirstOrDefault(a => a.AetheryteId == node.FallbackAetheryteId));
        }

        if (best.Entry == null) {
            toast.ShowError(Strings.T("NoAetheryte"));
            return;
        }

        string name = best.Entry.AetheryteData.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() ?? Strings.T("TheAetheryte");

        if (Telepo.Instance()->Teleport(best.Entry.AetheryteId, (byte)best.Entry.SubIndex)) {
            Framework.Service<IChatGui>().Print(Strings.T("Teleporting", name, best.Entry.GilCost, node.ItemName), Strings.T("ChatSource"));
        }
    }

    private static void OpenMap(TimedNode? node)
    {
        if (node == null || node.MapId == 0) return;

        Framework.DalamudFramework.Run(() => {
            Framework.Service<IGameGui>().OpenMapWithMapLink(node.TerritoryId, node.MapId, node.WorldPosition);
        });
    }

    private static string JobName(GatherJob job) => Strings.T(job == GatherJob.Miner ? "miner" : "botanist");

    private static string FormatWindows(TimedNode node) =>
        string.Join(", ", node.Windows.Select(w => {
            int end = (w.StartMinute + w.DurationMinutes) % 1440;
            return $"{w.StartMinute / 60:00}:{w.StartMinute % 60:00}–{end / 60:00}:{end % 60:00}";
        }));
}

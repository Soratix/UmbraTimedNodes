using Dalamud.Game;

namespace Umbra.TimedNodes;

/// <summary>
/// Textes du widget en français et en anglais.
/// Umbra ne permet pas aux widgets tiers d'ajouter leurs clés à sa propre table I18N :
/// on suit la langue d'Umbra (I18N.GetCurrentLanguage : réglage d'Umbra, sinon langue de
/// l'interface Dalamud), et à défaut celle du client du jeu. Français si « fr », anglais sinon.
/// </summary>
internal static class Strings
{
    public static bool IsFrench
    {
        get {
            try {
                string lang = I18N.GetCurrentLanguage();
                if (!string.IsNullOrEmpty(lang)) return lang.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
            } catch {
                // I18N indisponible : on se rabat sur la langue du client.
            }

            try {
                return Framework.Service<IClientState>().ClientLanguage == ClientLanguage.French;
            } catch {
                return false;
            }
        }
    }

    private static readonly Dictionary<string, (string Fr, string En)> Table = new() {
        ["WidgetName"]   = ("Nœuds temporaires", "Timed Nodes"),
        ["WidgetDesc"]   = ("Affiche le prochain nœud de récolte limité, légendaire ou éphémère, avec un compte à rebours. Cliquez pour la liste complète.",
                            "Shows the next unspoiled, legendary or ephemeral gathering node, with a countdown. Click for the full list."),
        ["Loading"]      = ("Chargement…", "Loading…"),
        ["LoadError"]    = ("Erreur de chargement", "Loading error"),

        // Réglages
        ["Cfg.TrackMode"]          = ("Nœuds affichés", "Displayed nodes"),
        ["Cfg.TrackMode.Desc"]     = ("« Nœuds suivis » : uniquement les nœuds cochés dans l'onglet « Nœuds suivis ». « Tous » : utilise les filtres de métier, niveau et mots-clés.",
                                      "\"Tracked nodes\": only the nodes ticked in the \"Tracked nodes\" tab. \"All\": uses the job, level and keyword filters."),
        ["Cfg.TrackMode.Tracked"]  = ("Uniquement les nœuds suivis (cochés)", "Tracked nodes only (ticked)"),
        ["Cfg.TrackMode.All"]      = ("Tous les nœuds (avec filtres)", "All nodes (with filters)"),
        ["Cfg.Job"]                = ("Métier (mode Tous)", "Job (All mode)"),
        ["Cfg.Job.Desc"]           = ("Quels nœuds afficher en mode « Tous ».", "Which nodes to show in \"All\" mode."),
        ["Cfg.Job.All"]            = ("Mineur et botaniste", "Miner and Botanist"),
        ["Cfg.MinLevel"]           = ("Niveau minimum (mode Tous)", "Minimum level (All mode)"),
        ["Cfg.MaxLevel"]           = ("Niveau maximum (mode Tous)", "Maximum level (All mode)"),
        ["Cfg.Ephemeral"]          = ("Inclure les nœuds éphémères (mode Tous)", "Include ephemeral nodes (All mode)"),
        ["Cfg.Ephemeral.Desc"]     = ("Nœuds éphémères utilisés pour l'aethérologie.", "Ephemeral nodes used for aetherial reduction."),
        ["Cfg.NameFilter"]         = ("Filtre par mots-clés (mode Tous)", "Keyword filter (All mode)"),
        ["Cfg.NameFilter.Desc"]    = ("Mots-clés séparés par des virgules. Seuls les nœuds dont un objet ou la zone contient un mot-clé sont suivis. Vide = tous.",
                                      "Comma-separated keywords. Only nodes whose item or zone contains a keyword are tracked. Empty = all."),
        ["Cfg.MaxEntries"]         = ("Nombre d'entrées dans la liste", "Number of entries in the list"),
        ["Cfg.ClickAction"]        = ("Action au clic sur un nœud", "Action when clicking a node"),
        ["Cfg.ClickAction.Desc"]   = ("Ce qui se passe quand vous cliquez sur un nœud de la liste (ou faites un clic droit sur le widget).",
                                      "What happens when you click a node in the list (or right-click the widget)."),
        ["Cfg.ClickAction.TeleportFlag"] = ("Drapeau + carte + téléportation à l'aethérite la plus proche", "Flag + map + teleport to the nearest aetheryte"),
        ["Cfg.ClickAction.Teleport"]     = ("Téléporter à l'aethérite la plus proche", "Teleport to the nearest aetheryte"),
        ["Cfg.ClickAction.Map"]          = ("Ouvrir la carte", "Open the map"),
        ["Cfg.ChatAlert"]          = ("Alerte dans le chat", "Chat alert"),
        ["Cfg.ChatAlert.Desc"]     = ("Affiche un message dans le chat quand un nœud affiché apparaît.", "Prints a chat message when a displayed node spawns."),
        ["Cfg.TrackedNodes"]       = ("Nœuds suivis", "Tracked nodes"),
        ["Cfg.Node"]               = ("{0} (niv. {1}{2}) — {3}", "{0} (Lv. {1}{2}) — {3}"),
        ["Cfg.Node.Ephemeral"]     = (", éphémère", ", ephemeral"),
        ["Cfg.Node.Desc"]          = ("Objets : {0} · Horaires ET : {1}", "Items: {0} · ET windows: {1}"),

        // Métiers et niveaux
        ["Miner"]           = ("Mineur", "Miner"),
        ["Botanist"]        = ("Botaniste", "Botanist"),
        ["miner"]           = ("mineur", "Miner"),
        ["botanist"]        = ("botaniste", "Botanist"),
        ["Level"]           = ("niv. {0}", "Lv. {0}"),
        ["LevelRange"]      = ("niv. {0}-{1}", "Lv. {0}-{1}"),

        // Barre, infobulle et menu
        ["NoTrackedNode"]   = ("Aucun nœud suivi : cochez-en dans les réglages", "No tracked node: tick some in the settings"),
        ["NoMatch"]         = ("Aucun nœud ne correspond aux filtres", "No node matches the filters"),
        ["SubActive"]       = ("{0} · reste {1}", "{0} · {1} left"),
        ["SubUpcoming"]     = ("{0} · dans {1}", "{0} · in {1}"),
        ["Tooltip"]         = ("{0} (niv. {1}, {2})\n{3} ({4:0.0}, {5:0.0})\nObjets : {6}\nHoraires ET : {7}\n\nClic : liste · Clic droit : {8}",
                               "{0} (Lv. {1}, {2})\n{3} ({4:0.0}, {5:0.0})\nItems: {6}\nET windows: {7}\n\nClick: list · Right-click: {8}"),
        ["Hint.Map"]        = ("ouvrir la carte", "open the map"),
        ["Hint.Teleport"]   = ("se téléporter au premier nœud", "teleport to the first node"),
        ["AltActive"]       = ("Actif · reste {0}", "Active · {0} left"),
        ["AltUpcoming"]     = ("Dans {0}", "In {0}"),

        // Chat et notifications
        ["ChatSource"]      = ("Nœuds temporaires", "Timed Nodes"),
        ["ChatSpawned"]     = ("{0} est disponible : {1} ({2:0.0}, {3:0.0}) — niv. {4} {5}", "{0} is up: {1} ({2:0.0}, {3:0.0}) — Lv. {4} {5}"),
        ["CannotTeleport"]  = ("Téléportation impossible pour le moment.", "Cannot teleport right now."),
        ["ZoneNotFound"]    = ("Zone introuvable.", "Zone not found."),
        ["NoAetheryte"]     = ("Aucune aethérite harmonisée pour rejoindre ce nœud.", "No attuned aetheryte to reach this node."),
        ["TheAetheryte"]    = ("l'aethérite", "the aetheryte"),
        ["Teleporting"]     = ("Téléportation vers {0} ({1} gils) pour {2}.", "Teleporting to {0} ({1} gil) for {2}."),
    };

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var entry)) return key;
        return IsFrench ? entry.Fr : entry.En;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);
}

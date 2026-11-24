# Umbra – Timed Nodes

[Français](README.md) · **English**

A toolbar widget for [Umbra](https://github.com/una-xiv/umbra) (FINAL FANTASY XIV, via Dalamud) that tracks **timed gathering nodes**: unspoiled, legendary and ephemeral.

## Features

- **In the toolbar:** the next node, with its item, zone and a live countdown. A progress bar shows the time left on an active node.
- **Tooltip:** level, gathering class, coordinates, gatherable items and spawn times in Eorzea time.
- **Click:** the list of nodes, active ones first in green.
- **Click on a node:** places a map flag, opens the map and teleports you to the nearest attuned aetheryte. This behaviour is configurable.
- **Tracked nodes:** tick only the nodes you care about, grouped by level range, alternating Miner and Botanist.
- **"All" mode:** filters by class, level and keywords.
- **Chat alert** when a node spawns (optional).

The data (nodes, times, positions) comes straight from the game files, in your client's language.

The widget's interface, name included, follows Umbra's language: French when Umbra is in French, English otherwise.

## Installation

1. In game, open **Umbra settings → Plugins**.
2. Add a GitHub repository: owner `Soratix`, repository `UmbraTimedNodes`.
3. Install the plugin. Umbra restarts its framework.
4. Switch the toolbar to edit mode and add the **Timed Nodes** widget (« Nœuds temporaires » when Umbra is in French). You can also search for `node` or `gathering`.
5. In the widget settings, set **Displayed nodes** to **Tracked nodes only (ticked)**, then tick your nodes in the **Tracked nodes** tab.

Umbra offers the update whenever a new release is published here.

## Building it yourself

You need the .NET 10 SDK and XIVLauncher with Umbra installed: the references point to `%AppData%\XIVLauncher`.

```powershell
dotnet build -c Release TimedNodes\TimedNodes.csproj
```

The DLL is built to `TimedNodes\bin\Release\Umbra.TimedNodes.dll`. After an Umbra update, change `UmbraVersion` in `TimedNodes.csproj` if the install folder has changed.

## License

AGPL-3.0, like Umbra.

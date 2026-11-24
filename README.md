# Umbra – Nœuds temporaires

**Français** · [English](README.en.md)

Plugin pour [Umbra](https://github.com/una-xiv/umbra) (FINAL FANTASY XIV, via Dalamud) qui ajoute un widget de barre d'outils pour suivre les **nœuds de récolte temporaires** : limités, légendaires et éphémères.

## Fonctionnalités

- **Dans la barre :** le prochain nœud, avec son objet, sa zone et un compte à rebours en temps réel. Une barre de progression montre le temps restant d'un nœud actif.
- **Infobulle :** le niveau, le métier, les coordonnées, les objets récoltables et les horaires en heure d'Eorzéa.
- **Clic :** la liste des nœuds, les actifs en vert en premier.
- **Clic sur un nœud :** pose un drapeau, ouvre la carte et vous téléporte à l'aethérite harmonisée la plus proche. Ce comportement se règle.
- **Nœuds suivis :** cochez uniquement les nœuds qui vous intéressent, rangés par tranche de niveau, mineur et botaniste en alternance.
- **Mode « Tous » :** des filtres par métier, niveau et mots-clés.
- **Alerte dans le chat** quand un nœud apparaît (optionnelle).

Les données (nœuds, horaires, positions) viennent directement des fichiers du jeu, dans la langue du client.

L'interface du widget est en français si Umbra (ou, à défaut, le client du jeu) est en français, et en anglais sinon.

## Installation

1. Dans le jeu, ouvrez les **paramètres d'Umbra → Plugins**.
2. Ajoutez un dépôt GitHub : propriétaire `Soratix`, dépôt `UmbraTimedNodes`.
3. Installez le plugin. Umbra redémarre son framework.
4. Passez la barre d'outils en mode édition et ajoutez le widget **« Nœuds temporaires »** (« Timed Nodes » en anglais ; la liste des widgets affiche les deux noms). Dans la recherche, vous pouvez aussi taper `récolte` ou `node`.
5. Dans les réglages du widget, mettez **« Nœuds affichés »** sur **« Uniquement les nœuds suivis »**, puis cochez vos nœuds dans l'onglet **« Nœuds suivis »**.

Umbra propose la mise à jour quand une nouvelle release est publiée ici.

## Compiler soi-même

Il faut le SDK .NET 10 et XIVLauncher avec Umbra installé : les références pointent vers `%AppData%\XIVLauncher`.

```powershell
dotnet build -c Release TimedNodes\TimedNodes.csproj
```

La DLL est générée dans `TimedNodes\bin\Release\Umbra.TimedNodes.dll`. Après une mise à jour d'Umbra, changez `UmbraVersion` dans `TimedNodes.csproj` si le dossier d'installation a changé.

## Licence

AGPL-3.0, comme Umbra.

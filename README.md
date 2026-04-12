# Velune

Velune est une application desktop cross-platform inspirée d’Aperçu, construite avec .NET et Avalonia.

## Structure

- `Velune.App` : bootstrap et shell Avalonia
- `Velune.Presentation` : ViewModels, commandes, logique de présentation
- `Velune.Application` : cas d’usage
- `Velune.Domain` : modèles métier et contrats
- `Velune.Infrastructure` : implémentations techniques
- `Velune.Tests.*` : tests unitaires, intégration, rendu

## Architecture

- `Presentation -> Application`
- `Application -> Domain`
- `Infrastructure -> Application + Domain`
- `App` compose l’ensemble

## Métriques Dev

En mode `Development`, Velune journalise les métriques MVP au niveau `Information`.

- `MVP metric | DocumentOpen` : temps d’ouverture document, type, taille, nombre de pages et mémoire approximative.
- `MVP metric | FirstPageRender` : temps du premier rendu visible avec `RenderDurationMs` et `TimeToFirstPageMs`.
- `MVP metric | ThumbnailRender` : temps de génération d’une miniature, dimensions de sortie et mémoire approximative.

Les indications mémoire sont volontairement approximatives et proviennent de `GC.GetTotalMemory(false)` et `Process.WorkingSet64`, convertis en Mo pour rester lisibles dans les logs de dev.

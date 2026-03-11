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
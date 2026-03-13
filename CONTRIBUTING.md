# Contributing

## Principes

Le projet suit une architecture en couches :

- `Velune.Presentation -> Velune.Application`
- `Velune.Application -> Velune.Domain`
- `Velune.Infrastructure -> Velune.Application + Velune.Domain`
- `Velune.App` compose les dépendances

Le domaine ne dépend d’aucune technologie UI ou bibliothèque externe.

## Règles de code

- `nullable` doit rester activé
- les warnings sont traités comme des erreurs
- toute opération longue doit accepter un `CancellationToken`
- aucune logique métier dans les Views
- aucune librairie externe ne doit remonter dans `Presentation`
- préférer des types immuables quand c’est pertinent
- éviter les méthodes longues et les ViewModels trop volumineux

## Style

- indentation : 4 espaces
- fins de ligne : LF
- accolades obligatoires
- `var` seulement quand le type est évident
- noms explicites et orientés métier

## Tests

- `Velune.Tests.Unit` : domaine et application
- `Velune.Tests.Integration` : infrastructure
- `Velune.Tests.Render` : rendu et snapshots

## Pull requests

Avant d’ouvrir une PR :

- lancer `dotnet build`
- lancer `dotnet test`
- vérifier le formatage
- vérifier que les dépendances entre couches restent propres

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

## Tests

Les tests unitaires couvrent la base métier et les cas d’usage critiques dans `tests/Velune.Tests.Unit`.

Commande locale simple :

```bash
dotnet test tests/Velune.Tests.Unit/Velune.Tests.Unit.csproj
```

Les tests de rendu restent disponibles séparément dans `tests/Velune.Tests.Render`.

Les tests d’intégration couvrent aussi l’ouverture, le rendu minimal et désormais la chaîne OCR/recherche texte dans `tests/Velune.Tests.Integration`.

Pour les scénarios OCR locaux, `tesseract` doit être disponible sur la machine.

```bash
dotnet test tests/Velune.Tests.Integration/Velune.Tests.Integration.csproj
```

Les snapshots de rendu sont stockés dans `tests/Velune.Tests.Render/Snapshots/Approved`.

Stratégie de comparaison :

- rendu piloté par un host desktop dédié pour rester au plus proche du pipeline réel
- comparaison PNG RGBA déterministe
- image tournée : comparaison stricte avec tolérance par canal `<= 2` et pixels différents `<= 0.1%`
- PDF page : comparaison avec voisinage `1px`, tolérance par canal `<= 12` et pixels différents `<= 1.5%`
- PDF miniature : comparaison avec voisinage `1px`, tolérance par canal `<= 14` et pixels différents `<= 3%`
- les profils PDF absorbent les petites variations de rasterisation entre plateformes tout en restant assez stricts pour détecter un mauvais rendu

Commande locale :

```bash
dotnet test tests/Velune.Tests.Render/Velune.Tests.Render.csproj
```

## CI

Le workflow GitHub Actions est défini dans `.github/workflows/ci.yml`.

- une `pull request` déclenche un restore + build sur `ubuntu`, `windows` et `macOS`
- les tests `unit` et `integration` sont exécutés sur `ubuntu-latest`
- le job de tests Ubuntu installe `qpdf` et `tesseract-ocr` avant les suites d’intégration
- les snapshots de rendu sont exécutés sur `macos-latest` pour garder une baseline PDF déterministe
- un échec de build ou de test remonte directement dans les checks GitHub de la PR

## OCR

Le cadrage technique OCR et recherche texte est documenté dans [`docs/ocr-text-search.md`](docs/ocr-text-search.md).

## Qualité

Des workflows dédiés sont aussi présents pour renforcer la qualité et la sécurité :

- [`.github/workflows/codeql.yml`](.github/workflows/codeql.yml) : analyse GitHub CodeQL sur `csharp`
- [`.github/workflows/semgrep.yml`](.github/workflows/semgrep.yml) : scan Semgrep CE avec upload SARIF vers GitHub code scanning
- [`.github/workflows/sonarqube-cloud.yml`](.github/workflows/sonarqube-cloud.yml) : analyse SonarQube Cloud pour .NET

Configuration minimale :

- `SONAR_TOKEN` dans les secrets GitHub
- `SONAR_PROJECT_KEY` et `SONAR_ORGANIZATION` dans les variables GitHub
- `ENABLE_GITHUB_CODE_SCANNING=true` dans les variables GitHub si le dépôt est privé avec GitHub Code Security activé

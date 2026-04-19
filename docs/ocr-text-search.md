# OCR et recherche texte

## Objectif
Velune vise un premier lot OCR et recherche texte :

- 100% local
- hors-ligne
- sans service cloud
- sans modifier le document source en v1
- commun pour PDF et images

Le but du v1 n'est pas de produire un nouveau PDF OCRisé. Le but est de rendre le document recherchable dans Velune avec une UX claire et non bloquante.

## Recommandation
La cible retenue pour Velune est :

- PDF textuels : extraction directe via PDFium
- PDF scannés : OCR local via Tesseract CLI sur des rendus de page
- Images : OCR local via Tesseract CLI
- recherche texte : panneau latéral dédié, navigation de résultats, surlignage dans le viewer
- persistance : cache disque des index texte/OCR avec invalidation explicite

Cette option garde l'app légère, cohérente entre plateformes et compatible avec l'architecture déjà en place.

## Comparatif des options

### 1. Tesseract CLI + index géré par Velune
Statut : recommandé

Points forts :

- totalement local et offline
- supporte PDF scannés et images avec la même chaîne métier
- licences simples et bien connues
- s'intègre bien au pipeline existant PDFium + rendu image
- laisse Velune maître du cache, des coordonnées de texte et de l'UX

Points faibles :

- Velune doit gérer l'orchestration OCR
- Velune doit gérer le cache disque et l'invalidation
- Velune doit gérer l'indexation, les extraits de recherche et les rectangles de surlignage

Conclusion :

- meilleur compromis pour un viewer documentaire desktop local

### 2. OCRmyPDF + Tesseract
Statut : non retenu pour v1

Points forts :

- très bon choix si le besoin devient de produire un PDF OCRisé exportable
- pipeline mature pour enrichir un PDF scanné avec une couche texte

Points faibles :

- ajoute une stack Python
- ajoute des dépendances de distribution plus lourdes
- introduit une dépendance indirecte autour de Ghostscript qui demande une vigilance supplémentaire sur la distribution et la licence
- plus orienté transformation de document que simple expérience de recherche dans le viewer

Conclusion :

- intéressant plus tard si Velune doit exporter des PDF OCRisés
- trop lourd pour le besoin produit immédiat

### 3. OCR natif par OS
Statut : non retenu

Exemples :

- Apple Vision
- OCR Windows

Points forts :

- qualité potentiellement très bonne selon la plateforme
- intégration système potentiellement élégante

Points faibles :

- comportements divergents selon les OS
- complexifie fortement la matrice de test
- Windows OCR introduit des contraintes de packaging et d'identité applicative
- rend plus difficile une expérience homogène entre macOS, Linux et Windows

Conclusion :

- séduisant sur une plateforme donnée
- pas adapté à une base cross-platform cohérente pour Velune

### 4. OCR cloud
Statut : rejeté

Points forts :

- qualité potentiellement élevée
- moins d'effort local sur certains moteurs

Points faibles :

- contraire au choix produit local/offline
- ajoute coût et complexité opérationnelle
- introduit des questions de confidentialité et de conformité
- dégrade la prédictibilité de l'expérience utilisateur

Conclusion :

- incompatible avec la direction produit retenue pour Velune

## Contraintes de licence

### Tesseract
- moteur open source sous licence Apache 2.0
- compatible avec une intégration locale dans Velune

### Tessdata
- modèles entraînés distribués publiquement avec Tesseract
- la distribution exacte doit rester alignée avec les fichiers choisis et leur licence associée

### OCRmyPDF
- logiciel libre mais adossé à une pile plus large
- le sujet n'est pas OCRmyPDF lui-même mais le coût opérationnel et le risque de distribution autour des dépendances annexes

### Ghostscript
- nécessite une vigilance spécifique si un futur flux d'export OCRisé est adopté
- ce point renforce le choix de ne pas le prendre comme base du v1

## Impacts architecture

### Couche texte dédiée
Le texte doit rester découplé du rendu bitmap. Le viewer doit pouvoir :

- charger du texte embarqué
- lancer une analyse OCR séparée
- annuler les jobs obsolètes
- persister les résultats indépendamment du rendu visuel

Interfaces retenues :

- `IDocumentTextService`
- `IOcrEngine`
- `IDocumentTextCache`
- `IDocumentTextAnalysisOrchestrator`

### Modèle commun de texte
Le modèle texte doit être identique pour PDF et images :

- `DocumentTextIndex`
- `PageTextContent`
- `TextRun`
- `NormalizedTextRegion`
- `TextSourceKind`

Les rectangles sont stockés en coordonnées normalisées. C'est le choix le plus stable pour supporter :

- zoom
- rotation
- fit to page
- fit to width
- tailles de rendu différentes

### Orchestration
L'analyse texte suit la même philosophie que le rendu :

- jobs asynchrones
- annulation
- purge des jobs obsolètes
- aucune dépendance au thread UI

### Cache disque
Le cache doit être invalidé si l'un des éléments suivants change :

- chemin du document
- taille du fichier
- date de modification
- type de document
- moteur OCR
- version moteur
- langues OCR
- mode forcé OCR ou texte embarqué

Le document source n'est jamais muté dans ce lot.

## Impacts UX

### Ouverture du panneau
Le panneau de recherche s'ouvre via :

- bouton de toolbar
- `Cmd/Ctrl+F`

### PDF textuel
- la recherche est disponible immédiatement
- aucun OCR implicite n'est lancé

### PDF scanné et image
- l'utilisateur voit un CTA explicite `Recognize text`
- l'app n'impose pas une analyse silencieuse à l'ouverture
- l'OCR peut être annulé

### Résultats
Le panneau doit afficher :

- nombre de résultats
- position dans la sélection
- extraits lisibles
- navigation précédent/suivant
- ouverture d'un résultat

Le viewer doit afficher :

- un surlignage indépendant du bitmap
- recalculé sur zoom, rotation et changement de page

### Gestion d'erreur
Les erreurs doivent rester utilisateur :

- pas de stack trace brute
- pas de détail technique exposé dans l'UI
- fallback clair si `tesseract` n'est pas installé

## Plan de déploiement retenu

### Étape 1
- extraction texte PDF via PDFium
- panneau de recherche
- résultats et navigation

### Étape 2
- OCR images via Tesseract
- cache disque OCR
- invalidation

### Étape 3
- OCR PDF scannés via rendu page puis Tesseract
- surlignage dans le viewer

### Étape 4
- couverture d'intégration et CI avec prérequis `tesseract`

## Direction produit verrouillée
Pour Velune, la bonne direction est :

- PDFium pour le texte embarqué
- Tesseract CLI pour l'OCR local
- cache disque maîtrisé par l'application
- aucune dépendance cloud
- aucune réécriture du document source en v1

OCRmyPDF reste une option future seulement si le besoin devient :

- exporter un PDF OCRisé
- enrichir structurellement un document source pour d'autres outils

## Références

- [Tesseract documentation](https://tesseract-ocr.github.io/tessdoc/)
- [Tesseract trained data licensing](https://github.com/tesseract-ocr/tessdata)
- [OCRmyPDF documentation](https://ocrmypdf.readthedocs.io/)
- [OCRmyPDF license on PyPI](https://pypi.org/project/ocrmypdf/)
- [Ghostscript licensing FAQ](https://ghostscript.com/faq)
- [PDFium text extraction API](https://pdfium.googlesource.com/pdfium/%2B/main/public/fpdf_text.h)
- [Apple Vision text recognition](https://developer.apple.com/documentation/vision/vnrecognizetextrequest)
- [Windows OCR package identity constraint](https://learn.microsoft.com/en-us/windows/uwp/api/windows.media.ocr)

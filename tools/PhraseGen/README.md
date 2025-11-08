# PhraseGen

Génère jusqu'à ~10k lignes de voix (phrases) pour Virgil, en shards JSON placés dans `src/Virgil.App/assets/voice/fr/generated`.

## Utilisation
```bash
dotnet run -c Release --project tools/PhraseGen\PhraseGen.csproj 
```
Optionnel: passer un chemin de sortie en argument.

## Notes
- Les templates sont volontairement simples et sûrs (placeholders textuels).
- À enrichir avec vos propres dictionnaires pour un ton unique.
- Le moteur côté app lit tous les `*.json` du dossier `assets/voice/fr`.

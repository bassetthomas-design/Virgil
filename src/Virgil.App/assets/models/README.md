# Modèles locaux (GGUF)

Déposez ici le modèle LLM au format GGUF utilisé par Virgil.

- Exemple : `virgil-model.Q4_K_M.gguf`
- Le modèle doit être embarqué dans le package offline (aucun téléchargement runtime).
- Si aucun modèle n'est présent, l'application doit utiliser un fallback local (rule-based) pour éviter tout crash.

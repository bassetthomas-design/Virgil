# Capabilities V3 (Virgil)

Ce dossier contient la **source de vérité** des actions exécutables par Virgil (V3).

- `capabilities.v3.json` : catalogue des actions (ID, niveau, domaine, dry-run, rollback, risque, params)
- `ai_request.schema.json` : schéma JSON de la requête envoyée à l'IA locale
- `ai_response.schema.json` : schéma JSON de la réponse IA (plan = IDs)

Règle d’or : l’IA propose, le moteur déterministe exécute (allowlist + dry-run + rollback + logs).

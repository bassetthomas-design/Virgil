# Contribuer à Virgil

Bienvenue dans le projet **Virgil — Assistant Système Intelligent**.

Ce document explique comment participer efficacement au développement du projet.

---

## 🧱 Structure du projet

| Dossier | Contenu principal |
|----------|------------------|
| `src/` | Code source de Virgil (App, Core, Agent, Services) |
| `tests/` | Tests unitaires et d’intégration |
| `docs/` | Documentation technique et architecture |

---

## ⚙️ Environnement
- **.NET 8.0 (Windows)** minimum.
- SDK complet installé (inclut WPF & Windows Forms).
- Vérifiez avec `dotnet --list-sdks`.
- IDE recommandé : Visual Studio 2022 ou JetBrains Rider.

---

## 🧩 Workflow Git

1. Créez une branche à partir de `main` :
   ```bash
   git checkout -b feature/nom-fonctionnalite
   ```
2. Commitez clairement :
   ```bash
   git commit -m "feat(ui): ajout effet Thanos sur chatbox"
   ```
3. Poussez la branche :
   ```bash
   git push origin feature/nom-fonctionnalite
   ```
4. Ouvrez une **Pull Request** vers `main`.
5. La CI doit passer **verte** avant merge.

---

## 💡 Standards de code
- Respecter le style C# officiel (Microsoft).
- `var` autorisé si le type est évident.
- Préfixes `I` pour les interfaces.
- Pas de logique dans les constructeurs lourds (préférez `Initialize`).

---

## 🧪 Tests
- Tous les modules doivent avoir des tests unitaires.
- Utiliser `xUnit` pour les tests simples.
- Commande :
  ```bash
  dotnet test
  ```

---

## 💬 Communication
- Discussions techniques via issues GitHub.
- Pour les PR : "Ready for review" quand testée et documentée.

---

Merci pour votre contribution ! ❤️

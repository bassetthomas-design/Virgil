# Virgil — Assistant Système Intelligent (Spécifications finales)

## 1) Concept général
Virgil est un assistant PC intelligent et vivant, mêlant entretien complet, mises à jour, monitoring et interactions humaines.  
Il veille, commente, conseille et agit : un compagnon système pro, humain et réactif.

- Interface élégante avec avatar expressif (humeurs dynamiques)
- Communication exclusivement textuelle sous forme de chat animé
- Autonomie intelligente : Virgil sait quand et comment nettoyer, mettre à jour et alerter

## 2) Interface (UI/UX)
### Barre du haut
- Horloge temps réel (HH:mm:ss)
- Bouton **Surveillance ON/OFF** (toggle)
- Affichage d’état (“Surveillance activée” / “Surveillance arrêtée”)

### Panneau gauche
- Avatar **Virgil** (visuel animé, expressions selon humeur et état système)
- Jauges : **CPU, GPU, RAM, Disque**
- Températures : **CPU, GPU, Disque** (seuils colorés)
- Animation de “fatigue” / “stress” en cas de charge forte

### Zone de chat
Canal unique où Virgil s’exprime et commente tout :
- Actions utilisateur (“Lancement du nettoyage complet, accroche-toi !”)
- Résultats (“PC nettoyé à 98 %, tu peux respirer.”)
- Punchlines et anecdotes humoristiques
- États internes (“Je me sens chaud, 72°C au CPU, pas mal non ?”)

### Zone actions
Boutons :
- **Maintenance complète**
- **Nettoyage intelligent**
- **Nettoyer navigateurs**
- **Tout mettre à jour**
- **Defender (MAJ + Scan)**
- **Ouvrir configuration**

## 3) Intelligence comportementale
### Surveillance
- Rafraîchissement toutes les 1–2 s (CPU/GPU/RAM/Disques + températures)
- Virgil **commente** la situation (humour, inquiétude, réassurance)
- **Humeurs dynamiques** : `happy`, `focused`, `warn`, `alert`, `sleepy`, `proud`, `tired`, etc.

### Punchlines automatiques
- Toutes les **1 à 6 minutes** pendant la surveillance
- Tirées d’une bibliothèque aléatoire (système, météo, humeur, humour)

## 4) Nettoyage intelligent
### Mode intelligent (auto)
Virgil **analyse l’état** du système et choisit le type de nettoyage :
- **Simple** : peu de fichiers, session courte, peu de déchets
- **Complet** : espace disque faible, usage prolongé, RAM saturée, historique important
- **Approfondi (Pro)** : système “pollué” (caches lourds, logs, fragments)

### Étendue (niveau Pro, style CCleaner)
- Corbeille
- Dossiers **TEMP** (user + Windows)
- **Prefetch**, logs, caches divers
- Restes d’installations, fichiers obsolètes
- **Caches navigateurs** (Chrome, Edge, Brave, Opera, OperaGX, Vivaldi, Firefox)
- **Caches système** : DirectX Shader, Windows Error Reports, Adobe, etc.
- Fichiers vides, dossiers résiduels
- Suppression contrôlée + statistiques (trouvés/supprimés)

### Effet “Thanos” (visuel)
Lors d’un **nettoyage complet** :
- Compte à rebours **60 s** avant “désintégration”
- Animation visuelle (désintégration dans l’UI, particules)
- Chat :
  - “Prépare-toi. Dans 60 secondes, les fichiers inutiles disparaîtront…”
  - “Je sens la poussière s’évaporer. 💨”
  - “Nettoyage complet terminé. L’univers est équilibré à nouveau.”

## 5) Mises à jour totales
### Action “Tout mettre à jour”
- **Applications + Jeux** : `winget upgrade --all --include-unknown`
- **Pilotes** : détection OEM + packages winget (best effort)
- **Windows Update** : Scan → Download → Install (UsoClient)
- **Microsoft Defender** : signatures + **scan rapide** post-install
- Vérification dépendances (ex. .NET, redists)

### Commentaires (chat)
- “Je prépare les mises à jour système.”
- “Applications et jeux : c’est parti.”
- “Windows Update en cours, pas touche à la souris.”
- “Pilotes mis à jour. Ça tourne comme un Razer neuf.”
- “Tout est à jour, je me sens rajeuni.”

## 6) Maintenance complète (scénario global)
Enchaîne :
1. **Nettoyage intelligent total**
2. **Nettoyage navigateurs**
3. **Mises à jour globales** (apps/jeux/pilotes/Windows/Defender)

Affiche un **rapport final** dans le chat :
- “Mode maintenance activé.”
- “Suppression des résidus... rafraîchissement en cours.”
- “Nettoyage terminé, passe un bon moment sans poussière.”

## 7) Configuration & personnalisation
- **Fusion machine + user** (user override)
- Paramètres :
  - Seuils CPU/RAM/Températures
  - Type de punchlines
  - Fréquences (surveillance, nettoyage, punchlines)
  - Style des messages (professionnel / humoristique)
  - Thème visuel (clair/sombre, humeur)

## 8) Journalisation & logs
- Journal interne pour toutes les actions (timestamp, durée, résultats)
- **Résumé** dans le chat
- **Log complet** : `%AppData%\Virgil\logs\YYYY-MM-DD.log`

## 9) Sécurité & robustesse
- Pas de vocal (chat uniquement)
- Tolérance aux erreurs (aucun crash)
- Vérifie **droits admin** avant opérations sensibles
- Garde-fous (pas de suppression système critique)
- Message clair en cas de refus UAC / échec d’accès

## 10) Humanisation
- Virgil **parle tout le temps** : commente, plaisante, exprime ses “émotions”
- Mini-mémoire temporaire de contexte (“Tu n’avais pas lancé un scan hier ?”)
- Humeur affichée dans l’avatar et les bulles

## 11) Résumé express — ce que Virgil fait
- ✅ Surveille CPU/GPU/RAM/Températures en direct  
- ✅ Parle en chat (humain, drôle, vivant)  
- ✅ Change d’humeur selon le système  
- ✅ Nettoie tout (corbeille → caches → prefetch → logs → navigateurs → système)  
- ✅ Nettoie **intelligemment** selon l’état du PC  
- ✅ Met à jour **tout** (apps, jeux, pilotes, Windows, Defender)  
- ✅ Effets visuels (Thanos, humeurs, progression)  
- ✅ Journalise tout (chat + fichiers logs)  
- ✅ Gère erreurs, droits et alertes  
- ✅ 100 % texte, **pas de vocal**

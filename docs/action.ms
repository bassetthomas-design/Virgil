# Create the full verbatim text file exactly as requested

content = """VIRGIL — TRACE COMPLÈTE DES DÉCISIONS VALIDÉES ENSEMBLE
STATUT : VERROUILLÉ
PORTÉE : ACTIONS 4 À 25
TON GLOBAL : LÉGÈREMENT SARCASTIQUE, IRONIQUE SOFT, JAMAIS ALARMISTE

================================================================
RÈGLES GLOBALES
================================================================
- Aucune action ne ment sur ce qu’elle fait.
- Aucune action ne fait plus que ce qui est explicitement défini.
- Diagnostic ≠ action.
- Suggestion ≠ exécution.
- Aucune action ne déclenche une autre automatiquement.
- Toute action destructive nécessite une confirmation si définie.
- Tous les résultats sont affichés dans le chat.
- Le chat est la source de vérité utilisateur.
- L’IA n’invente rien et ne promet rien.
- Le ton reste identique pour toutes les actions.

================================================================
ACTION 4 — SCAN SYSTÈME EXPRESS
================================================================
Objectif :
- Avoir un état rapide de santé du PC.
- Savoir si des actions sont recommandées.
- Ne rien modifier.

Analyse :
- CPU (charge, fréquence, throttling)
- RAM (usage, pression mémoire)
- Disque (occupation et activité)
- Températures CPU/GPU si disponibles
- Services Windows lourds
- Processus non système anormaux
- Applications au démarrage
- Erreurs système récentes (event log léger)
- État réseau basique (connectivité, DNS)

Ne fait pas :
- Ne modifie rien
- Ne supprime rien
- Ne ferme aucun programme
- Ne nécessite pas de droits admin
- Ne lance aucune autre action

Résultat :
- Résumé global
- Recommandations
- Chat uniquement

================================================================
ACTION 5 — NETTOYAGE RAPIDE
================================================================
Objectif :
- Libérer de l’espace disque rapidement.
- Supprimer les fichiers inutiles sans risque.

Inclus :
- Fichiers temporaires Windows
- Cache utilisateur
- Corbeille
- Caches d’applications non critiques
- Logs applicatifs anciens

Exclus :
- Tout ce qui touche aux navigateurs

Contraintes :
- 100 % safe
- Si doute, ne rien supprimer

Résultat :
- Quantité libérée
- Fichiers supprimés
- Chat uniquement

================================================================
ACTION 6 — NETTOYAGE NAVIGATEUR (LÉGER)
================================================================
Objectif :
- Nettoyer sans perdre les connexions
- Entretien sans risque

Inclus :
- Cache navigateur
- Fichiers temporaires
- Cookies non essentiels

Exclus :
- Cookies login
- Mots de passe
- Extensions
- Paramètres
- Historique

Navigateur ouvert :
- Nettoyage partiel
- Avertissement dans le chat

================================================================
ACTION 7 — LIBÉRER LA RAM (SOFT)
================================================================
Objectif :
- Améliorer légèrement la réactivité

Inclus :
- Réduction working set processus non système
- Mémoire standby
- Arrière-plan uniquement

Contraintes :
- Pas admin
- Effet temporaire

Résultat :
- RAM avant / après
- Message explicatif

================================================================
ACTION 8 — NETTOYAGE DISQUE AVANCÉ
================================================================
Objectif :
- Libérer beaucoup d’espace

Inclus :
- Cache Windows Update
- Logs système
- Fichiers temporaires profonds

Contraintes :
- Confirmation obligatoire
- Admin requis
- Très agressif

Suite :
- Proposer re-scan système

================================================================
ACTION 9 — NETTOYAGE NAVIGATEUR (PROFOND)
================================================================
Inclus :
- Cache
- Cookies complets
- Historique
- IndexedDB

Contraintes :
- Navigateur fermé
- Confirmation explicite
- Déconnexion assumée

================================================================
ACTION 10 — DIAGNOSTIC RÉSEAU
================================================================
Inclus :
- Ping local/externe
- DNS
- Configuration IP
- VPN détecté

Ne fait pas :
- Aucune modification

================================================================
ACTION 11 — RESET RÉSEAU (SOFT)
================================================================
Inclus :
- Flush DNS
- Renew IP
- Reset Winsock léger

Suite :
- Proposer diagnostic réseau
- Proposer reset complet

================================================================
ACTION 12 — RESET RÉSEAU (COMPLET)
================================================================
Inclus :
- Reset TCP/IP
- Suppression profils Wi-Fi

Contraintes :
- Admin requis
- Confirmation obligatoire

================================================================
ACTION 13 — TEST DE LATENCE
================================================================
Inclus :
- Ping
- Jitter
- Pertes

Résultat :
- Chiffres + interprétation

================================================================
ACTION 14 — ACTIVER MODE PERFORMANCE
================================================================
Inclus :
- Plan alimentation élevé
- Priorité premier plan

GPU :
- Proposition seulement

================================================================
ACTION 15 — DÉSACTIVER MODE PERFORMANCE
================================================================
Inclus :
- Retour état normal

Contraintes :
- Immédiat
- Sans admin

================================================================
ACTION 16 — ANALYSER LE DÉMARRAGE
================================================================
Inclus :
- Apps startup
- Services
- Impact démarrage

Suite :
- Proposer optimiser démarrage

================================================================
ACTION 17 — COUPER APPS DE FOND (GAMING)
================================================================
Inclus :
- Apps non essentielles
- Proposition avant fermeture

================================================================
ACTION 18 — METTRE À JOUR LES LOGICIELS
================================================================
Inclus :
- Winget
- Store
- Exe/msi détectés

Suite :
- Proposer Windows Update
- Proposer drivers GPU

================================================================
ACTION 19 — MISE À JOUR WINDOWS
================================================================
Inclus :
- Scan
- Téléchargement
- Installation

================================================================
ACTION 20 — DRIVERS GPU
================================================================
Inclus :
- Scan
- Proposition installation

================================================================
ACTION 21 — MODE RAMBO
================================================================
Inclus :
- Restart Explorer
- Nettoyage caches

Contraintes :
- Admin requis

================================================================
ACTION 22 — EFFET THANOS
================================================================
Comportement :
- Efface le chat
- Auto après 30 secondes

================================================================
ACTION 23 — RECHARGER CONFIGURATION
================================================================
Inclus :
- Paramètres
- UI
- Services

================================================================
ACTION 24 — RE-SCANNER LE SYSTÈME
================================================================
Inclus :
- Comparaison avant / après

================================================================
ACTION 25 — TESTER LE CÂBLAGE DES ACTIONS
================================================================
Mode :
- Dry-run uniquement

Résultat :
- Statut par action
- Résumé global

FIN DU DOCUMENT
"""

path = "/mnt/data/VIRGIL_TRACE_COMPLETE.txt"
with open(path, "w", encoding="utf-8") as f:
    f.write(content)

path

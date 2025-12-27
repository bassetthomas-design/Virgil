# Installer GitHub CLI sur Debian/Ubuntu

Ce guide décrit comment installer l’outil en ligne de commande GitHub CLI (`gh`) à partir du dépôt officiel. Les commandes ci‑dessous doivent être exécutées sur une machine Debian/Ubuntu avec des privilèges sudo.

## Étapes

1. Mettre à jour les paquets et installer les dépendances nécessaires :

   ```bash
   sudo apt-get update
   sudo apt-get install -y curl gpg
   ```

2. Ajouter la clé du dépôt GitHub CLI et déclarer la source APT officielle :

   ```bash
   sudo mkdir -p -m 755 /etc/apt/keyrings
   curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
     | sudo tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null
   sudo chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg

   echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
     | sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null
   ```

3. Installer GitHub CLI et vérifier la version :

   ```bash
   sudo apt-get update
   sudo apt-get install -y gh

   gh --version
   ```

## Script complet

Pour exécuter toutes les étapes d’un seul tenant (avec arrêt en cas d’erreur), vous pouvez utiliser le script suivant :

```bash
set -e

sudo apt-get update
sudo apt-get install -y curl gpg

sudo mkdir -p -m 755 /etc/apt/keyrings
curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
  | sudo tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null
sudo chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
  | sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null

sudo apt-get update
sudo apt-get install -y gh

gh --version
```

Après l’installation, vous pouvez exécuter `gh auth login` pour connecter l’outil à votre compte GitHub.

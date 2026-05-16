# Configuration Nahel

## Où mettre `nahel.json`

Nahel cherche le fichier de config dans cet ordre :

1. Le chemin explicite donné avec `--config mon/chemin/nahel.json`
2. `nahel.json` dans le répertoire courant
3. `nahel.json` à côté de l'exécutable `nahel`
4. `%USERPROFILE%\.nahel\nahel.json` (Windows)  
   `~/.nahel/nahel.json` (Linux/macOS)

## Structure minimale

```json
{
  "Nahel": {
    "Host": "127.0.0.1",
    "Port": 11435,
    "DashboardEnabled": true,
    "OpenAiCompatibilityEnabled": true,
    "RequireApiKeyOnLan": true,
    "ApiKey": "local",
    "AllowUnauthenticatedLan": false
  },
  "Engines": {},
  "Models": {},
  "Tools": {}
}
```

## Déclarer un engine OVMS

```json
{
  "Engines": {
    "ovms": {
      "Type": "ovms",
      "DisplayName": "OpenVINO Model Server",
      "Enabled": true,
      "AutoStartPolicy": "ManualOnly",
      "ExecutablePath": "ovms",
      "WorkingDirectory": "",
      "ConfigPath": "ovms_config.json",
      "RestPort": 8000,
      "GrpcPort": 9000,
      "OpenAiProxyPort": 8080,
      "OpenVinoVersion": "2026.1",
      "VersionPolicy": "Official",
      "EnvironmentVariables": {
        "OVMS_LOG_LEVEL": "INFO"
      }
    }
  }
}
```

### `AutoStartPolicy`
- `Never` — jamais démarré automatiquement
- `ManualOnly` — démarré uniquement via API/CLI (`POST /engine/ovms/start`)
- `OnServerStart` — démarré automatiquement quand `nahel start` est lancé
- `OnFirstRequest` — démarré à la première requête qui l'utilise *(non implémenté encore)*

## Déclarer un modèle

```json
{
  "Models": {
    "qwen-fast": {
      "EngineId": "ovms",
      "DisplayName": "Qwen Fast",
      "EngineModelName": "qwen_fast",
      "ModelPath": "models/qwen-fast",
      "ContextSize": 8192,
      "LoadPolicy": "OnFirstRequest",
      "UnloadPolicy": "AfterIdleTimeout",
      "IdleTimeoutSeconds": 900,
      "Preload": false,
      "Enabled": true
    }
  }
}
```

## Déclarer un tool (Codex, Claude, etc.)

```json
{
  "Tools": {
    "codex": {
      "Enabled": true,
      "ExecutablePath": "codex",
      "Arguments": "",
      "WorkingDirectory": "",
      "AutoStart": false,
      "EnvironmentVariables": {
        "OPENAI_BASE_URL": "http://127.0.0.1:11435/v1",
        "OPENAI_API_KEY": "local"
      }
    },
    "claude": {
      "Enabled": true,
      "ExecutablePath": "claude",
      "Arguments": "",
      "WorkingDirectory": "",
      "AutoStart": false,
      "EnvironmentVariables": {
        "ANTHROPIC_BASE_URL": "http://127.0.0.1:11435",
        "ANTHROPIC_API_KEY": "local"
      }
    }
  }
}
```

## Variables d'environnement

Toute valeur de `nahel.json` peut être surchargée par une variable d'environnement préfixée par `NAHEL_`.

Exemples :
- `NAHEL_Nahel__Host=0.0.0.0`
- `NAHEL_Engines__ovms__ExecutablePath=C:\Tools\ovms\ovms.exe`
- `NAHEL_Engines__ovms__AutoStartPolicy=OnServerStart`

## Sécurité LAN

Par défaut Nahel écoute sur `127.0.0.1`.

Pour ouvrir sur le réseau local :
```bash
nahel start --lan
```

Si `--lan` est utilisé **et** qu'aucune `ApiKey` forte n'est configurée, un warning est affiché.  
Pour forcer l'accès LAN sans clé API :
```json
{ "Nahel": { "AllowUnauthenticatedLan": true } }
```

Tu es un agent développeur senior C#/.NET spécialisé en architecture modulaire, runtime local, serveurs ASP.NET Core, SDK publics, plugins, wrappers de moteurs IA locaux, OpenAI-compatible APIs, SignalR, process supervision et intégration Windows/Linux.

Contexte projet
===============

Le projet s’appelle Nahel.

Nahel signifie :
Node-based Agent Harmony and Execution Layer.

Mais la vision réelle est plus simple :
Nahel est une micro-coquille serveur agnostique pour orchestrer des moteurs IA locaux.

Nahel n’est pas un moteur d’inférence.
Nahel n’est pas Ollama.
Nahel n’est pas OVMS.
Nahel n’est pas llama.cpp.
Nahel est une couche d’orchestration, de standardisation, de supervision et d’exposition API autour de plusieurs engines IA locaux.

Objectif principal
==================

Créer une base complète C#/.NET pour Nahel avec :

1. Un SDK public contenant uniquement les contrats, DTO, routes, erreurs et politiques.
2. Un serveur runtime ASP.NET Core Minimal API.
3. Une CLI permettant de lancer le serveur avec `nahel start`.
4. Un système d’engines abstraits via interfaces.
5. Une implémentation initiale d’un engine OVMS.
6. Une architecture prévue pour accueillir plus tard llama.cpp, Ollama, LM Studio, OpenVINO GenAI, etc.
7. Une API locale pour supervision.
8. Une API OpenAI-compatible minimale.
9. Un dashboard web minimal avec SignalR pour logs/status.
10. Une queue interne de jobs pour les actions longues.
11. Une séparation stricte entre SDK, Server, Engines et CLI.

Très important :
Ne pas faire de MAUI.
Ne pas faire de marketplace.
Ne pas faire de système de plugins dynamique complexe.
Ne pas auto-charger tous les plugins trouvés.
Ne pas lancer un engine simplement parce qu’un plugin existe.
Ne pas implémenter de tests dans cette phase.
Ne pas polluer avec des fichiers de tests.
Ne pas surcomplexifier.

Architecture cible
==================

Solution :

Nahel.sln

Projects :

src/Nahel.SDK
src/Nahel.Server
src/Nahel.Cli
src/Nahel.Engine.Ovms
src/Nahel.Engine.LlamaCpp.Abstractions ou placeholder optionnel
src/Nahel.Shared optionnel uniquement si nécessaire

Le SDK doit être indépendant.
Le SDK ne doit dépendre d’aucun engine.
Le SDK ne doit pas connaître OVMS.
Le SDK ne doit pas connaître OpenVINO.
Le SDK ne doit pas contenir de logique métier lourde.
Le SDK expose le vocabulaire public de Nahel.

Nahel.Server référence Nahel.SDK.
Nahel.Engine.Ovms référence Nahel.SDK.
Nahel.Cli référence Nahel.Server et Nahel.SDK.

Nahel.Engine.Ovms doit être une implémentation concrète mais isolée.

Vision runtime
==============

Nahel fonctionne ainsi :

Client externe
  -> API OpenAI-compatible /v1/*
  -> Nahel.Server
  -> Engine sélectionné
  -> Engine OVMS / autre engine
  -> moteur réel

Dashboard web
  -> /dashboard
  -> SignalR hub
  -> logs/status/jobs/models/engines

CLI
  -> nahel start
  -> lance Nahel.Server
  -> charge config
  -> active engines enabled=true
  -> démarre seulement engines avec AutoStartPolicy=OnServerStart
  -> expose API + dashboard

Concepts fondamentaux
=====================

Il faut séparer :

plugin présent
plugin activé
engine configuré
engine démarré
modèle enregistré
modèle chargé
modèle utilisé

Ne jamais confondre ces états.

Un engine installé ne doit rien faire tout seul.
Un engine enabled=false ne doit pas démarrer.
Un modèle Preload=false ne doit pas être chargé au démarrage.
Un modèle peut être déclaré sans être chargé.

Le serveur est le chef d’orchestre.
Le worker exécute les commandes.
Les engines exposent leurs capacités.
Les politiques disent ce qui doit être fait.
La config est la source de vérité.

Configuration
=============

Créer un fichier appsettings.json ou nahel.json avec une structure claire :

{
  "Nahel": {
    "Host": "127.0.0.1",
    "Port": 11435,
    "DashboardEnabled": true,
    "OpenAiCompatibilityEnabled": true,
    "RequireApiKeyOnLan": true,
    "ApiKey": "local"
  },
  "Engines": {
    "ovms": {
      "Type": "ovms",
      "DisplayName": "OpenVINO Model Server",
      "Enabled": true,
      "AutoStartPolicy": "ManualOnly",
      "ExecutablePath": "C:\\Tools\\ovms\\ovms.exe",
      "WorkingDirectory": "C:\\Tools\\ovms",
      "ConfigPath": "C:\\Tools\\nahel\\ovms\\config.json",
      "RestPort": 8000,
      "GrpcPort": 9000,
      "OpenAiProxyPort": 8080,
      "OpenVinoVersion": "2026.1",
      "OvmsVersionPolicy": "Official",
      "EnvironmentVariables": {
        "OVMS_LOG_LEVEL": "INFO"
      }
    }
  },
  "Models": {
    "qwen-fast": {
      "EngineId": "ovms",
      "DisplayName": "Qwen Fast",
      "EngineModelName": "qwen_fast",
      "ModelPath": "C:\\Models\\qwen-fast",
      "ContextSize": 8192,
      "LoadPolicy": "OnFirstRequest",
      "UnloadPolicy": "AfterIdleTimeout",
      "IdleTimeoutSeconds": 900,
      "Preload": false,
      "Enabled": true
    }
  }
}

Ne pas hardcoder les chemins.
Prévoir Windows et Linux.
Utiliser Path.Combine autant que possible.
Ne jamais supposer C:\ sauf dans config exemple.

SDK
===

Créer Nahel.SDK.

Namespaces recommandés :

Nahel.SDK.Abstractions
Nahel.SDK.Contracts
Nahel.SDK.Models
Nahel.SDK.Routes
Nahel.SDK.Errors
Nahel.SDK.Policies
Nahel.SDK.Jobs
Nahel.SDK.Events

Contrats à créer :

IEngine
IEngineInstaller
IEngineUpdater
IEngineHealthClient
IModelRegistry
IModelSwitcher
IOpenAiCompatibleEngine
IEngineCapabilitiesProvider
IEngineLogSource

IEngine :

public interface IEngine
{
    string EngineId { get; }
    string DisplayName { get; }
    string EngineType { get; }

    Task<EngineStatus> GetStatusAsync(CancellationToken ct = default);
    Task<EngineHealth> GetHealthAsync(CancellationToken ct = default);
    Task<EngineCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task RestartAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
    Task<ModelSwitchResult> SwitchModelAsync(ModelSwitchRequest request, CancellationToken ct = default);
}

IEngineInstaller :

public interface IEngineInstaller
{
    Task<EngineInstallStatus> GetInstallStatusAsync(CancellationToken ct = default);
    Task<EngineInstallResult> InstallAsync(EngineInstallRequest request, CancellationToken ct = default);
    Task<EngineVerifyResult> VerifyAsync(CancellationToken ct = default);
}

IEngineUpdater :

public interface IEngineUpdater
{
    Task<EngineVersionInfo> GetVersionAsync(CancellationToken ct = default);
    Task<EngineUpdateResult> UpdateEngineAsync(EngineUpdateRequest request, CancellationToken ct = default);
    Task<EngineUpdateResult> UpdateRuntimeAsync(EngineUpdateRequest request, CancellationToken ct = default);
}

IOpenAiCompatibleEngine :

public interface IOpenAiCompatibleEngine
{
    Task<OpenAiModelListResponse> ListOpenAiModelsAsync(CancellationToken ct = default);
    Task<OpenAiChatCompletionResponse> CreateChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default);
    IAsyncEnumerable<OpenAiChatCompletionChunk> StreamChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default);
}

IModelRegistry :

public interface IModelRegistry
{
    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken ct = default);
    Task<ModelInfo?> GetModelAsync(string modelId, CancellationToken ct = default);
    Task RegisterModelAsync(ModelRegistrationRequest request, CancellationToken ct = default);
    Task RemoveModelAsync(string modelId, CancellationToken ct = default);
}

DTOs à créer
============

EngineInfo
EngineStatus
EngineHealth
EngineCapabilities
EngineVersionInfo
EngineInstallStatus
EngineInstallRequest
EngineInstallResult
EngineVerifyResult
EngineUpdateRequest
EngineUpdateResult

ModelInfo
ModelRuntimeInfo
ModelRegistrationRequest
ModelSwitchRequest
ModelSwitchResult
ModelRuntimePolicy
ModelLoadState

JobInfo
JobRequest
JobResult
JobStatus
JobType
JobEvent

LogEvent
EngineEvent
EngineStatusChangedEvent
ModelChangedEvent

OpenAiModelInfo
OpenAiModelListResponse
OpenAiChatCompletionRequest
OpenAiChatMessage
OpenAiChatCompletionResponse
OpenAiChatChoice
OpenAiChatCompletionChunk
OpenAiUsage

Politiques
==========

Créer enums :

EngineAutoStartPolicy
- Never
- ManualOnly
- OnServerStart
- OnFirstRequest

ModelLoadPolicy
- Never
- ManualOnly
- OnEngineStart
- OnFirstRequest
- KeepWarm

ModelUnloadPolicy
- Never
- ManualOnly
- AfterIdleTimeout
- OnMemoryPressure

EngineUpdatePolicy
- Never
- ManualOnly
- NotifyOnly
- AutoPatch
- AutoMinor

EngineRestartPolicy
- Never
- OnFailure
- OnUpdate
- ManualOnly

Créer ModelRuntimePolicy :

public sealed record ModelRuntimePolicy
{
    public ModelLoadPolicy LoadPolicy { get; init; } = ModelLoadPolicy.OnFirstRequest;
    public ModelUnloadPolicy UnloadPolicy { get; init; } = ModelUnloadPolicy.AfterIdleTimeout;
    public TimeSpan? IdleTimeout { get; init; } = TimeSpan.FromMinutes(15);
    public bool Preload { get; init; }
    public bool KeepWarm { get; init; }
    public bool AllowConcurrentRequests { get; init; } = true;
    public int MaxConcurrentRequests { get; init; } = 1;
    public int Priority { get; init; }
}

Capabilities
============

Créer EngineCapabilities :

public sealed record EngineCapabilities
{
    public bool SupportsMultiModel { get; init; }
    public bool SupportsHotSwap { get; init; }
    public bool SupportsUnloadModel { get; init; }
    public bool SupportsConcurrentModels { get; init; }
    public bool SupportsOpenAiApi { get; init; }
    public bool SupportsOllamaApi { get; init; }
    public bool SupportsStreaming { get; init; }
    public bool SupportsEmbeddings { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsUpdate { get; init; }
    public bool SupportsRuntimeUpdate { get; init; }
    public bool SupportsWarmup { get; init; }
    public bool SupportsMetrics { get; init; }
}

Routes SDK
==========

Créer NahelRoutes :

public static class NahelRoutes
{
    public const string Health = "/health";
    public const string Version = "/version";

    public const string Dashboard = "/dashboard";

    public const string Engines = "/engine";
    public const string EngineStatus = "/engine/{engineId}/status";
    public const string EngineHealth = "/engine/{engineId}/health";
    public const string EngineStart = "/engine/{engineId}/start";
    public const string EngineStop = "/engine/{engineId}/stop";
    public const string EngineRestart = "/engine/{engineId}/restart";
    public const string EngineCapabilities = "/engine/{engineId}/capabilities";
    public const string EngineLogs = "/engine/{engineId}/logs";

    public const string EngineModels = "/engine/{engineId}/models";
    public const string EngineRegisterModel = "/engine/{engineId}/models/register";
    public const string EngineSwitchModel = "/engine/{engineId}/models/switch";
    public const string EngineUnloadModel = "/engine/{engineId}/models/{modelId}/unload";

    public const string EngineUpdate = "/engine/{engineId}/update";
    public const string RuntimeUpdate = "/engine/{engineId}/runtime/update";

    public const string Jobs = "/jobs";
    public const string JobById = "/jobs/{jobId}";

    public const string OpenAiModels = "/v1/models";
    public const string OpenAiChatCompletions = "/v1/chat/completions";
    public const string OpenAiCompletions = "/v1/completions";
    public const string OpenAiEmbeddings = "/v1/embeddings";

    public const string SignalREngineHub = "/hubs/engine";
}

Erreurs
=======

Créer :

EngineException
EngineNotFoundException
EngineNotEnabledException
EngineStartException
EngineStopException
EngineUpdateException
ModelNotFoundException
ModelLoadException
ModelSwitchException
UnsupportedEngineCapabilityException

Créer EngineError :

public sealed record EngineError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string? EngineId { get; init; }
    public string? ModelId { get; init; }
    public Dictionary<string, string> Details { get; init; } = new();
}

Nahel.Server
============

Créer serveur ASP.NET Core Minimal API.

Fonctions :
- Charger configuration.
- Enregistrer engines configurés.
- Ne démarrer que les engines enabled=true et AutoStartPolicy=OnServerStart.
- Exposer routes management.
- Exposer routes OpenAI-compatible.
- Exposer dashboard minimal.
- Exposer SignalR hub.
- Exécuter actions longues via job queue.

Services internes :

IEngineCatalog
IEngineRuntime
IEngineCommandQueue
IJobStore
IEngineEventBus
IModelRouter
IOpenAiRouter

IEngineCatalog :
- contient les engines configurés
- ne déclenche pas de démarrage automatiquement

IEngineRuntime :
- démarre/stoppe/restart engine via queue ou direct selon action

IModelRouter :
- map model id public vers engine id + engine model name

IEngineCommandQueue :
- queue interne en mémoire
- exécution séquentielle par engine
- empêche collisions : pas de start/stop/update/switch simultanés sur même engine

Pas besoin de RabbitMQ.
Pas besoin de stockage DB.
Utiliser Channel<T> ou BackgroundService.

Jobs
====

Créer job queue simple :

Job types :
- StartEngine
- StopEngine
- RestartEngine
- RegisterModel
- SwitchModel
- UnloadModel
- UpdateEngine
- UpdateRuntime

Chaque job :
- JobId
- EngineId
- Type
- Status
- CreatedAt
- StartedAt
- CompletedAt
- Error
- Logs

Statuses :
- Queued
- Running
- Succeeded
- Failed
- Cancelled

Quand une route POST lance une action longue, elle retourne immédiatement :

{
  "jobId": "...",
  "status": "Queued"
}

SignalR diffuse les changements.

SignalR
=======

Créer EngineHub.

Events :
- EngineStatusChanged
- JobQueued
- JobStarted
- JobLog
- JobSucceeded
- JobFailed
- ModelRegistered
- ModelSwitched
- LogEvent

Créer un logger provider ou un service de log central qui pousse vers SignalR.

Ne pas mettre tous les logs en console.
La console doit rester sobre.
Le dashboard doit recevoir les logs.

Dashboard
=========

Créer dashboard minimal servi par Nahel.Server.
Pas besoin de React complexe.
Un HTML statique + CSS + JS suffit.
Ou Razor minimal si plus simple.

Dashboard doit afficher :
- status serveur
- engines
- modèles
- jobs
- logs live SignalR
- boutons start/stop/restart engine
- bouton switch model

Pas besoin de design parfait.
Inspiration : Aspire dashboard, mais version simple.

CLI
===

Créer Nahel.Cli.

Commandes minimales :

nahel start
nahel status
nahel stop
nahel open
nahel models
nahel engines

Options start :
--host
--port
--config
--lan
--no-dashboard

Comportement :

nahel start
- lance serveur
- dashboard activé par défaut
- bind local par défaut 127.0.0.1
- si --lan alors host 0.0.0.0
- si --lan et pas d’api key configurée, afficher warning ou refuser selon config

nahel open
- ouvre navigateur sur /dashboard

Nahel.Engine.Ovms
=================

Créer implémentation OVMS.

Ne pas inclure OVMS dans le repo.
Ne pas vendoriser OpenVINO.
Ne pas figer OpenVINO 2025.
Prévoir OpenVINO 2026.1.
OVMS et OpenVINO doivent rester officiels et updatables.

Classes :

OvmsEngine : IEngine, IEngineInstaller, IEngineUpdater
OvmsOptions
OvmsProcessSupervisor
OvmsConfigWriter
OvmsHealthClient
OvmsModelRegistry
OvmsModelSwitcher
OvmsOpenAiProxyClient optionnel
OvmsLogReader
OvmsVersionService

OvmsOptions :

public sealed record OvmsOptions
{
    public string EngineId { get; init; } = "ovms";
    public string DisplayName { get; init; } = "OpenVINO Model Server";
    public string ExecutablePath { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public string ConfigPath { get; init; } = "";
    public int RestPort { get; init; } = 8000;
    public int GrpcPort { get; init; } = 9000;
    public string OpenVinoVersion { get; init; } = "2026.1";
    public string VersionPolicy { get; init; } = "Official";
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}

OvmsProcessSupervisor :
- start process
- stop process
- restart process
- capture stdout/stderr
- push logs to event bus
- kill process tree on stop
- no shell execute
- set working dir
- set env vars

OvmsHealthClient :
- GET http://localhost:{RestPort}/v3/models
- parse model ids
- reachable true/false
- status

OvmsConfigWriter :
- génère config.json OVMS
- ne manipule pas seulement model_config_list[0]
- doit pouvoir générer une config multi-model
- atomic write
- backup
- rollback

S’inspirer du wrapper Python existant, mais ne pas copier aveuglément :
- garder lock
- garder atomic write
- garder backup/rollback
- garder wait readiness
- corriger limitation single model
- corriger source de vérité config.env

OVMS source of truth côté Nahel :
- Nahel config est source de vérité.
- OVMS config.json est généré.
- config.env n’est pas source de vérité.

OVMS capabilities :
- SupportsMultiModel = true
- SupportsHotSwap = true si config reload ou restart disponible
- SupportsOpenAiApi = selon proxy disponible
- SupportsStreaming = true si proxy ou OVMS endpoint compatible
- SupportsUpdate = true
- SupportsRuntimeUpdate = true
- SupportsMetrics = true si disponible

OpenAI compatibility
====================

Le serveur Nahel doit exposer :

GET /v1/models
POST /v1/chat/completions

GET /v1/models :
- retourne modèles activés dans Nahel
- format OpenAI-compatible

POST /v1/chat/completions :
- reçoit model
- route vers engine responsable
- si engine supporte IOpenAiCompatibleEngine, déléguer
- sinon retourner erreur claire

Streaming :
- prévoir structure
- implémentation minimale acceptée
- si stream=true mais engine ne supporte pas, retourner erreur ou fallback non-stream selon option

Important :
Ne pas chercher à implémenter toute l’API OpenAI.
Minimum viable seulement.

LAN
===

Par défaut :
- host 127.0.0.1

Si --lan :
- host 0.0.0.0
- dashboard accessible LAN
- API accessible LAN

Sécurité :
- si LAN actif, exiger ApiKey sauf config explicite AllowUnauthenticatedLan=true.
- CORS configurable.
- logs ne doivent pas exposer secrets.

Fichiers à générer
==================

Créer solution complète avec csproj.

Nahel.SDK.csproj :
- net8.0 ou netstandard2.1 si vraiment utile
- nullable enable
- implicit usings enable
- pas de dépendances lourdes

Nahel.Server.csproj :
- net8.0
- ASP.NET Core
- SignalR
- Microsoft.Extensions.Hosting
- System.Text.Json

Nahel.Cli.csproj :
- net8.0
- console
- référence Server
- parser simple sans dépendance lourde au début

Nahel.Engine.Ovms.csproj :
- net8.0
- référence SDK
- HttpClient
- process management

Style de code
=============

C# moderne.
Nullable enable.
Records pour DTO.
Interfaces propres.
Pas de logique cachée.
Pas de static global state inutile.
Pas de service locator.
Utiliser DI Microsoft.Extensions.DependencyInjection.
Utiliser ILogger.
Préférer async/await.
Support CancellationToken partout.
Éviter exceptions silencieuses.
Messages d’erreurs explicites.

Ne pas sur-architecturer.
Pas de plugin loader dynamique maintenant.
Pas de reflection magique.
Pas de système distribué.
Pas de DB.

Mais préparer les contrats pour que ça puisse évoluer.

Livrable attendu
================

Le repo doit compiler.

La CLI doit permettre :

nahel start

Le serveur doit exposer :

GET /health
GET /version
GET /engine
GET /engine/{engineId}/status
POST /engine/{engineId}/start
POST /engine/{engineId}/stop
GET /engine/{engineId}/models
POST /engine/{engineId}/models/switch
GET /v1/models
POST /v1/chat/completions
GET /dashboard
SignalR /hubs/engine

Le projet OVMS doit être implémenté suffisamment pour :
- lire options
- démarrer process OVMS si executable configuré
- arrêter process
- appeler /v3/models
- générer config.json
- reporter health
- lister modèles connus/configurés

Pas besoin que l’installation automatique OVMS soit parfaite maintenant.
Prévoir interfaces updater/installer mais implémentation peut être minimale avec NotImplementedException propre ou résultat Unsupported si non disponible.

Contraintes importantes
=======================

Ne pas coder MAUI.
Ne pas ajouter de tests.
Ne pas ajouter Docker.
Ne pas vendoriser OVMS.
Ne pas vendoriser OpenVINO.
Ne pas créer marketplace.
Ne pas charger automatiquement plugins non déclarés.
Ne pas rendre le système trop autonome.
Ne pas faire de magie.

Philosophie
===========

Nahel doit rester une coquille simple.

Il ne fait pas tout.
Il fait ce qu’on lui demande.
Il standardise les moteurs.
Il orchestre proprement.
Il expose des routes.
Il supervise.
Il découple.

OVMS n’est qu’une brique.
llama.cpp sera une autre brique.
Ollama sera une autre brique.
Le SDK est le contrat stable.
Le serveur est le runtime.
Les engines sont remplaçables.
Le dashboard est une fenêtre de supervision.

Implémente maintenant la structure complète et les fichiers principaux.
Privilégie une base propre, compilable, lisible, extensible.
Ne t’arrête pas après une esquisse : crée les projets, les interfaces, les DTO, les routes, les services principaux, les mappings DI et les endpoints minimaux.

Référence OVMS wrapper existante
===============================

Un repo communautaire a été identifié comme référence fonctionnelle :

https://github.com/codex-corp/intel-arc-ovms-interface

Ce repo ne doit pas être repris tel quel.
Il ne doit pas devenir une dépendance obligatoire.
Il sert de référence technique pour comprendre une orchestration OVMS existante.

Points importants observés :

- Le repo ne modifie pas le code source d’OVMS.
- Il orchestre OVMS officiel autour de scripts, config, registry et proxy.
- Il contient un module intéressant :
  tools/model_manager

Ce module contient notamment :
- model_registry.py
- ovms_config.py
- swap_service.py
- ovms_client.py
- manage_models.py
- file_lock.py
- swap_logger.py
- env_config.py

Analyse du module :

model_registry.py :
- lit un fichier JSON de registry
- retourne un mapping modelName -> modelPath

ovms_config.py :
- lit config.json OVMS
- extrait le modèle courant depuis model_config_list[0]
- construit une config modifiée
- écrit atomiquement le JSON
- fait backup/rollback

swap_service.py :
- orchestre le switch modèle
- lock fichier
- backup config
- écriture config OVMS
- update config.env
- attend que /v3/models expose le modèle
- rollback si timeout

ovms_client.py :
- appelle http://localhost:{port}/v3/models
- récupère les modèles chargés par OVMS

manage_models.py :
- expose une CLI status/list/switch/rollback

Conclusion :
Ce repo prouve que le wrapper est bien une couche d’orchestration autour d’OVMS, pas un fork d’OVMS.

À garder comme concepts :
- registry de modèles
- config writer OVMS
- atomic write
- backup/rollback
- file lock
- wait readiness via /v3/models
- status JSON
- switch model
- logs d’événements

À ne pas reprendre tel quel :
- logique Python comme runtime final
- config.env comme source de vérité
- limitation model_config_list[0]
- hardcode localhost
- hardcode version OpenVINO 2025.x
- fautes/chemins figés comme artficats
- hypothèses Windows uniquement si évitables
- couplage au layout exact du repo communautaire

Nahel.Engine.Ovms doit réimplémenter ces concepts proprement en C#.

Objectif OVMS 2026.1
====================

Ne pas figer OpenVINO/OVMS sur les versions du repo communautaire.

Le repo communautaire semble orienté OpenVINO 2025.x.
Nahel doit viser OpenVINO 2026.1 et OVMS officiel compatible.

Règle :
- OVMS officiel reste updatable.
- OpenVINO officiel reste updatable.
- Nahel ne doit pas vendoriser OVMS.
- Nahel ne doit pas vendoriser OpenVINO.
- Nahel doit permettre de remplacer OVMS par une version officielle plus récente.

Prévoir :
- OvmsVersionPolicy
- OpenVinoVersion
- EngineUpdatePolicy
- RuntimeUpdatePolicy
- VerifyOfficialRuntimeAsync
- DetectInstalledOvmsAsync
- DetectInstalledOpenVinoAsync

La voie finale :
Nahel.Engine.Ovms s’inspire du repo codex-corp, mais devient un wrapper C# propre, versionnable, agnostique et compatible avec OVMS/OpenVINO officiels.

Tu travail ? tu renomme les projet ici, tu peux virer le projet maui.
tu restructure en projet monorepo, on fera le changement plutard. tu peux canibaliser le repository actuel.
Si tu as accès tu peux meme le renommer en NahelAI









IMPORTant apres pause ! 


CORRECTION CRITIQUE — RÉCUPÉRER LE CŒUR OLLAMOCK DEPUIS GIT
============================================================

Le projet initial n’était pas seulement un runtime d’engines IA.

Le cœur historique du projet Ollamock était :

mocker Ollama
+
émuler ses commandes
+
permettre à Ollama de lancer/contrôler des outils comme Codex, Claude, etc.
+
servir de bridge/launcher compatible avec les usages attendus autour de `ollama`

Exemples d’usage cœur à préserver :

ollama start codex

ollama start claude

ollama run <model-or-tool>

ollama list

ollama ps

ollama stop <target>

Le développement actuel a trop supprimé cette intention.

Il faut récupérer depuis l’historique Git le code/structure lié à :

- mock Ollama
- commandes CLI Ollama-like
- launcher tools
- mapping commande -> outil
- lifecycle des outils externes
- process supervisor existant
- adaptation Codex
- adaptation Claude
- routes ou endpoints Ollama-compatible déjà prévus
- anciens fichiers supprimés utiles
- ancienne logique Ollamock

Instruction obligatoire :

Inspecter l’historique Git.

Utiliser :

git log --oneline --all

git status

git branch --all

git diff

git show <commit>:<path>

git checkout <commit> -- <path>

si nécessaire.

Ne pas inventer depuis zéro si le code existait déjà.

Récupérer le code utile depuis les commits précédents.

============================================================
NOUVEAU DÉCOUPAGE LOGIQUE À AJOUTER
============================================================

Ajouter un projet dédié au cœur Ollamock :

src/Ollamock.System

ou si le nom Nahel est conservé pour le runtime :

src/Nahel.Ollamock.System

Rôle de ce projet :

- préserver la logique historique Ollamock
- mocker Ollama
- exposer une couche CLI/commandes compatible Ollama
- gérer les outils externes comme Codex/Claude
- traduire commandes Ollama-like vers actions runtime
- gérer lifecycle process tools
- faire le lien entre compatibilité Ollama et engines/tools

Ce projet n’est PAS l’engine OVMS.

Ce projet n’est PAS le SDK général.

Ce projet est la couche “Ollamock compatibility system”.

============================================================
ARCHITECTURE CORRIGÉE
============================================================

La bonne architecture devient :

Nahel.SDK
    Contrats communs
    DTO
    routes
    interfaces engines
    interfaces tools
    policies
    errors

Nahel.Server
    runtime HTTP
    dashboard
    SignalR
    queue jobs
    API native
    API OpenAI-compatible
    API Ollama-compatible

Nahel.Engine.Ovms
    wrapper OVMS
    OpenVINO/OVMS officiel
    engine local
    models
    health
    update

Nahel.Engine.LlamaCpp
    futur engine GGUF/llama.cpp

Ollamock.System
    mock Ollama historique
    commandes Ollama-like
    launcher Codex
    launcher Claude
    mapping tools
    process lifecycle tools
    adaptation historique du repo

Nahel.Cli ou Ollamock.Cli
    expose commandes utilisateur
    start/status/run/stop/list
    peut déléguer à Ollamock.System

============================================================
DISTINCTION IMPORTANTE
============================================================

Il y a deux familles de choses à orchestrer :

1. Engines IA

Exemples :

OVMS
llama.cpp
Ollama réel
LM Studio
OpenVINO GenAI

Ces engines servent à faire de l’inférence.

2. Tools / Agents / CLIs externes

Exemples :

Codex
Claude CLI
autres assistants CLI
outils dev

Ces tools ne sont pas forcément des engines d’inférence.
Ils peuvent consommer une API locale.
Ils peuvent être lancés comme processus.
Ils peuvent avoir besoin d’un environnement spécifique.
Ils peuvent dépendre d’un endpoint compatible Ollama/OpenAI.

Ollamock.System doit gérer cette deuxième famille.

============================================================
CONTRATS À AJOUTER AU SDK
============================================================

Ajouter interfaces tool-level :

public interface IToolLauncher
{
    string ToolId { get; }
    string DisplayName { get; }

    Task<ToolStatus> GetStatusAsync(CancellationToken ct = default);

    Task<ToolLaunchResult> StartAsync(
        ToolLaunchRequest request,
        CancellationToken ct = default);

    Task<ToolStopResult> StopAsync(
        ToolStopRequest request,
        CancellationToken ct = default);
}

public interface IToolRegistry
{
    Task<IReadOnlyList<ToolInfo>> ListToolsAsync(
        CancellationToken ct = default);

    Task<ToolInfo?> GetToolAsync(
        string toolId,
        CancellationToken ct = default);
}

DTO à ajouter :

ToolInfo
ToolStatus
ToolLaunchRequest
ToolLaunchResult
ToolStopRequest
ToolStopResult
ToolRuntimePolicy
ToolEnvironmentVariable
ToolProcessInfo

============================================================
OLLAMA-LIKE COMMAND SYSTEM
============================================================

Ajouter concepts :

OllamaCommand
OllamaCommandParser
OllamaCommandRouter
OllamaCommandResult

Commandes à supporter conceptuellement :

start
run
stop
list
ps
pull
show
serve

Le but n’est pas de tout implémenter parfaitement maintenant.

Le but est de préserver le squelette historique.

Exemples :

ollamock start codex

doit devenir :

OllamaCommandParser
↓
OllamaCommandRouter
↓
ToolLauncher Codex
↓
ProcessSupervisor
↓
logs SignalR
↓
status

ollamock start claude

doit suivre le même chemin.

============================================================
PROCESS SUPERVISION TOOLS
============================================================

Récupérer ou recréer proprement :

ToolProcessSupervisor

Responsabilités :

- démarrer process externe
- arrêter process externe
- capturer stdout
- capturer stderr
- injecter variables d’environnement
- définir working directory
- suivre PID
- exposer status
- pousser logs vers SignalR/job log
- éviter double start
- gérer stop propre puis kill si timeout

Ce supervisor doit pouvoir être commun avec les engines si logique similaire,
mais ne pas mélanger Engine et Tool au niveau des contrats.

============================================================
CONFIGURATION TOOLS
============================================================

Ajouter config :

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

Important :

les tools doivent pouvoir pointer vers Nahel/Ollamock local comme endpoint.

============================================================
ROUTES À AJOUTER
============================================================

Ajouter routes management tools :

GET /tools

GET /tools/{toolId}/status

POST /tools/{toolId}/start

POST /tools/{toolId}/stop

GET /tools/{toolId}/logs

Ajouter dans NahelRoutes ou OllamockRoutes :

public const string Tools = "/tools";
public const string ToolStatus = "/tools/{toolId}/status";
public const string ToolStart = "/tools/{toolId}/start";
public const string ToolStop = "/tools/{toolId}/stop";
public const string ToolLogs = "/tools/{toolId}/logs";

============================================================
RÈGLE DE RÉCUPÉRATION
============================================================

Le code historique supprimé doit être recherché dans Git.

Priorité :

1. récupérer fichiers supprimés utiles
2. intégrer dans Ollamock.System
3. adapter namespaces proprement
4. ne pas écraser l’architecture Nahel
5. ne pas perdre le cœur mock Ollama

Ne pas réécrire de mémoire si le code existe dans l’historique.

============================================================
PHILOSOPHIE FINALE
============================================================

Nahel est la coquille runtime/SDK/server.

Ollamock.System est la compatibilité historique Ollama-like.

OVMS est un engine.

Codex/Claude sont des tools/agents externes.

Le projet doit conserver les deux axes :

1. orchestrer des engines IA locaux
2. mocker Ollama pour lancer/contrôler des outils comme Codex/Claude

Le deuxième axe est le cœur historique d’Ollamock et ne doit plus être supprimé.

Ne pas repartir de zéro.

Récupérer depuis Git.

Réintégrer proprement.

Structurer logiquement.
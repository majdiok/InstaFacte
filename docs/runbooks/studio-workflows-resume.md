# Runbook — Jobs Hangfire des workflows Studio : `studio-workflow-resume` et `studio-workflow-scheduled:*`

## Objet

Deux familles de jobs Hangfire font vivre les workflows Studio sans requête HTTP :

- le job récurrent **`studio-workflow-resume`** (15ᵉ descripteur de `HangfireRecurringJobsRegistrationService`)
  fait avancer les instances existantes : cron **`*/10 * * * *`** (UTC),
  `[DisableConcurrentExecution(timeoutInSeconds: 540)]`, `[AutomaticRetry(Attempts = 0)]` (un tick manqué est
  rattrapé au suivant, jamais de double exécution) — sections « Les 4 phases » à « Lecture des logs » ;
- les jobs récurrents **`studio-workflow-scheduled:{tenantId}:{definitionId}`** (un par workflow **actif** à
  déclencheur **Planifié**, v1.1 — 4.7b) démarrent de nouvelles instances selon le cron du workflow — section
  « Déclencheurs planifiés ».

Garde commune : si `Ollama:EnableStudioWorkflows` est `false`, chaque job journalise et sort immédiatement.

## Les 4 phases (par tenant actif, dans l'ordre)

1. **Reaper** — relâche les baux périmés (`LeasedAt < now − StudioWorkflowLeaseMinutes`) : une reprise
   morte en vol ne bloque jamais une instance plus de `LeaseMinutes`.
2. **Expirations** — chaque approbation `pending` échue (`DueAt ≤ now`) passe `expired` : statut mémorisé
   dans le contexte (`_approval.{étape}.status = "expired"`), l'instance est rendue **due sans changer
   d'étape** (D-05) — le moteur appliquera `onTimeout` à la reprise. Notification 16 au lanceur
   (« Approbation « … » expirée », lien `/studio/approvals`). Une course (décision utilisateur
   concurrente) isole l'approbation (warning), pas le tenant (D-26).
3. **Reprises** — instances `waiting`/`waiting_approval` échues **sans approbation encore `pending`**
   (D-04) : le runner pose le bail (`RowVersion`), impersonne le lanceur (fail-closed : rôle plateforme,
   rôle applicatif ambigu ou identifiants vides ⇒ refus) et appelle le moteur — les instances sans
   lanceur (déclencheur système) tournent sans impersonation. Lanceur introuvable ou
   inactif ⇒ instance `failed` « Lanceur introuvable ou inactif : reprise refusée. » + notification 17 +
   audit `Studio.Workflow.InstanceFailed`. Le bail est relâché en `finally` quoi qu'il arrive (D-25).
4. **Purge** — instances terminales (`completed`/`failed`/`cancelled`) dont `CompletedAt <
   now − StudioWorkflowRetentionDays` : suppressions enfants d'abord (exécutions d'étapes, approbations),
   puis l'instance. Non atomique par lot, sûre en pratique grâce à `DisableConcurrentExecution` (D-24).

## Déclencheurs planifiés (`studio-workflow-scheduled:*`, v1.1)

- **Identifiant** : `studio-workflow-scheduled:{tenantId:N}:{definitionId:N}` (`StudioWorkflowScheduleService.JobId`,
  GUID sans tirets). Le job est **créé ou mis à jour** (`AddOrUpdate`, `TimeZone = UTC`) à chaque écriture réussie
  d'une définition **active et planifiée** (création, modification, activation) et **retiré** (`RemoveIfExists`) à
  la désactivation, à la suppression ou au changement de type de déclencheur. Cron à 5 champs **UTC** relu de
  `TriggerConfigJson.cron` ; cron illisible ⇒ retrait du job + `Warning`, sans exception ; Hangfire indisponible ⇒
  l'écriture métier réussit quand même (erreur absorbée). **Aucune resynchronisation au démarrage** : si la base
  Hangfire est recréée, ré-enregistrer le workflow (ou basculer son interrupteur deux fois) pour recréer le job.
- **Tick** (`StudioWorkflowScheduledJob.FireAsync(tenantId, definitionId)`, `[DisableConcurrentExecution(540)]`,
  `[AutomaticRetry(Attempts = 0)]`) : drapeau coupé ⇒ sortie ; tenant sans chaîne de connexion ⇒ `Warning` « tick
  ignoré » ; définition absente, inactive ou non planifiée ⇒ **auto-guérison** (job retiré, `Information`
  « définition absente ou non planifiée — job retiré ») ; filtres illisibles ⇒ `Warning` « tick ignoré » ; sinon
  balayage des enregistrements filtrés par lot et **une instance système par fiche** (`StartedBy = null` — la
  reprise sans impersonation est gérée par le runner). Une fiche ayant déjà une instance **ouverte** de ce workflow
  (ou de sa chaîne) est ignorée ; le quota `MaxWorkflowInstancesPerRecord` s'applique ; une fiche en erreur
  n'interrompt pas les autres.
- **Lecture des compteurs** (`Information`, par tick) : `Studio workflow planifié {WorkflowId} (tenant {TenantId}) :
  {Scanned} enregistrement(s) balayé(s), {Started} instance(s) démarrée(s), {Skipped} ignorée(s), {Failed} échouée(s)`.
  `Warning` « lot de {Take} atteint sur {Total} enregistrement(s) correspondant(s) — la suite au prochain tick » :
  plus de fiches que `StudioWorkflowScheduledBatchSize` — normal sur un premier passage, à surveiller si permanent
  (augmenter le lot, resserrer les filtres ou espacer le cron). `Warning` « démarrage impossible {DefinitionId}
  {RecordId} » : une fiche isolée en échec (compteur `Failed`).
- **Inventaire des jobs** (SQL, schéma Hangfire `hangfire`) :

  ```sql
  SELECT s.[Value] AS JobId, h.[Field], h.[Value]
  FROM [hangfire].[Set] s
  LEFT JOIN [hangfire].[Hash] h ON h.[Key] = 'recurring-job:' + s.[Value]
  WHERE s.[Key] = 'recurring-jobs' AND s.[Value] LIKE 'studio-workflow-scheduled:%'
    AND h.[Field] IN ('Cron', 'NextExecution', 'LastExecution')
  ORDER BY s.[Value], h.[Field];
  ```

  Le tableau de bord `/hangfire` (onglet **Recurring jobs**) liste les mêmes entrées et permet **Trigger now** /
  **Delete**.
- **Retrait manuel** (`RemoveIfExists` via le tableau de bord, ou `IRecurringJobManager` dans une console) :
  job **orphelin** après suppression d'un tenant (la définition n'existe plus : le prochain tick l'auto-guérit,
  sauf si le tenant n'a plus de chaîne de connexion — le tick est alors ignoré à chaque échéance et le job
  reste : le retirer à la main) ; job **trop fréquent** (un cron « chaque minute » `* * * * *` est accepté, D-47-B02 /
  U5) : désactiver le workflow depuis le hub (le job est retiré) puis corriger le cron avant de réactiver.
- **Réglage** : `Ollama:StudioWorkflowScheduledBatchSize` (défaut 100, clamp 10..500) — fiches traitées par tick.

## Réglages (`Ollama:*`, clamps entre parenthèses)

| Clé | Défaut | Borne | Effet |
|---|---|---|---|
| `StudioWorkflowLeaseMinutes` | 30 | 5..120 | Durée du bail posé par le runner / seuil du reaper |
| `StudioWorkflowResumeBatchSize` | 100 | 10..500 | Taille de lot des 4 phases |
| `StudioWorkflowScheduledBatchSize` | 100 | 10..500 | Fiches traitées par tick d'un déclencheur planifié (v1.1) |
| `StudioWorkflowRetentionDays` | 180 | 30..3650 | Âge minimal des instances terminales purgées (et horizon de l'onglet Historique de « Mes approbations ») |
| `EnableStudioWorkflows` | `false` | — | Coupé ⇒ jobs inertes, routes runtime 404 |

## Lecture des logs

- Résumé chiffré par tenant (`Information`) : baux relâchés, approbations expirées, reprises par issue
  (`resumed` / `leaseBusy` / `starterUnavailable`), purgées. Aucun contenu (`ContextJson`, titres,
  commentaires) n'est jamais journalisé.
- `Reprise {InstanceId} tenant {TenantId} en échec` (`Error`) : une instance a levé une exception ; les
  autres instances et tenants ne sont pas affectés.
- `Expiration de l'approbation {ApprovalId} tenant {TenantId} en échec` (`Warning`) : course perdue ou
  panne isolée sur une expiration.
- `Contexte trop volumineux : statut « expired » perdu {ApprovalId}` (`Warning`) : la sérialisation du
  contexte a échoué (> 64 Ko) ; l'approbation est expirée mais le marqueur n'est pas dans le contexte.

## Questions fréquentes

**Une instance reste `waiting` alors que son échéance est passée.**
Un bail est probablement posé (reprise en cours ou morte en vol). Attendre un tick après
`StudioWorkflowLeaseMinutes` : le reaper relâche le bail et l'instance est reprise. Si elle doit être
abandonnée : `POST api/studio/workflows/instances/{id}/cancel` (policy `custom_records:write`).

**Le lanceur a été désactivé entre-temps.**
Au prochain tick l'instance passe `failed` avec l'erreur « Lanceur introuvable ou inactif : reprise
refusée. » ; le lanceur reçoit la notification 17 (« Workflow « … » en échec »). C'est voulu (fail-closed,
D-18) : réactiver l'utilisateur puis relancer manuellement depuis la fiche.

**Les approbateurs n'ont pas réagi.**
`POST api/studio/workflows/instances/{id}/remind` ré-émet les notifications des approbations en attente
(titre « Rappel : … »). Au plus une relance par 24 h (409 en deçà). Au-delà de l'échéance de l'étape, le
job expire l'approbation et applique `onTimeout`.

## Activation progressive

1. `Ollama:EnableStudioWorkflows=true` sur un **tenant pilote** (les autres tenants ne sont pas affectés :
   le job itère sur les tenants actifs mais les routes et les déclencheurs restent gardés).
2. Créer un workflow `manual` à une étape `wait` courte sur une table de test, le lancer depuis la fiche.
3. Après 2 ticks, vérifier qu'aucune instance n'est bloquée échue :

   ```sql
   SELECT COUNT(*) FROM StudioWorkflowInstances
   WHERE Status IN (1, 2) /* Waiting, WaitingApproval */ AND DueAt < DATEADD(minute, -30, SYSUTCDATETIME());
   -- attendu : 0 (hors baux en cours : LeasedAt récent)
   ```

4. Vérifier la boîte de réception (`GET api/studio/workflows/approvals/mine/count`) sur un compte assigné.

## Désactivation

`Ollama:EnableStudioWorkflows=false` : les jobs (`studio-workflow-resume` et chaque
`studio-workflow-scheduled:*`) sortent immédiatement à chaque tick, les **11 routes runtime**
(`StudioWorkflowRuntimeController` : boîte de réception, historique et compteur des approbations, approve / reject,
instances d'une fiche et détail, workflows lançables, run / cancel / remind) répondent `404` « Les workflows Studio
ne sont pas activés. » avant tout traitement. La route `GET api/studio/records/{entityKey}/{id}/history`
(historique d'une fiche, 4.7h2) n'est **pas** sous ce drapeau et reste servie. **Rien n'est supprimé** :
instances, approbations, définitions et jobs planifiés sont conservés et reprendront à la réactivation (les baux
résiduels sont moissonnés par le reaper).

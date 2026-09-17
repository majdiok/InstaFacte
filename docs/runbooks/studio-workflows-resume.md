# Runbook — Job `studio-workflow-resume` (exécution différée des workflows Studio)

## Objet

Le job récurrent Hangfire `studio-workflow-resume` (15ᵉ descripteur de
`HangfireRecurringJobsRegistrationService`) fait avancer les workflows Studio sans requête HTTP :
cron **`*/10 * * * *`** (UTC), `[DisableConcurrentExecution(timeoutInSeconds: 540)]`,
`[AutomaticRetry(Attempts = 0)]` (un tick manqué est rattrapé au suivant, jamais de double exécution).

Garde : si `Ollama:EnableStudioWorkflows` est `false`, le job journalise et sort immédiatement.

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
   rôle applicatif ambigu ou identifiants vides ⇒ refus) et appelle le moteur. Lanceur introuvable ou
   inactif ⇒ instance `failed` « Lanceur introuvable ou inactif : reprise refusée. » + notification 17 +
   audit `Studio.Workflow.InstanceFailed`. Le bail est relâché en `finally` quoi qu'il arrive (D-25).
4. **Purge** — instances terminales (`completed`/`failed`/`cancelled`) dont `CompletedAt <
   now − StudioWorkflowRetentionDays` : suppressions enfants d'abord (exécutions d'étapes, approbations),
   puis l'instance. Non atomique par lot, sûre en pratique grâce à `DisableConcurrentExecution` (D-24).

## Réglages (`Ollama:*`, clamps entre parenthèses)

| Clé | Défaut | Borne | Effet |
|---|---|---|---|
| `StudioWorkflowLeaseMinutes` | 30 | 5..120 | Durée du bail posé par le runner / seuil du reaper |
| `StudioWorkflowResumeBatchSize` | 100 | 10..500 | Taille de lot des 4 phases |
| `StudioWorkflowRetentionDays` | 180 | 30..3650 | Âge minimal des instances terminales purgées |
| `EnableStudioWorkflows` | `false` | — | Coupé ⇒ job inerte, routes runtime 404 |

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
   WHERE Status IN (1, 2) /* Running, Waiting */ AND DueAt < DATEADD(minute, -30, SYSUTCDATETIME());
   -- attendu : 0 (hors baux en cours : LeasedAt récent)
   ```

4. Vérifier la boîte de réception (`GET api/studio/workflows/approvals/mine/count`) sur un compte assigné.

## Désactivation

`Ollama:EnableStudioWorkflows=false` : le job sort immédiatement à chaque tick, les 9 routes runtime
répondent `404` « Les workflows Studio ne sont pas activés. » avant tout traitement. **Rien n'est
supprimé** : instances, approbations et définitions sont conservées et reprendront à la réactivation
(les baux résiduels sont moissonnés par le reaper).

# Assistant IA — optimisations de latence (mars 2026)

## Résumé des changements

### Backend — consultations métier (gain principal)
- **Parallélisme DB** : outils read-only exécutés en parallèle (`EnableParallelDbTools`, `MaxParallelDbTools`)
- **Cache read-only** : 45 s par tenant (`EnableReadOnlyToolCache`)
- **SQL agrégé / projection** : rapports CA, panier, performance produit, tendances, produits jamais vendus, marges commerciales, **TVA ventes**, **ventes par ligne**, **paiements clients** (projection sans entités complètes)
- **Catalogue d'outils** : dates/preset optionnels sur les outils `get_*` (moins d'appels `resolve_reporting_period`)
- **Dates par défaut** : outils `get_*` utilisent le mois en cours si `from_date`/`to_date` absents
- **Pré-traitement parallèle** : modèle + conversation + prompt système avant le 1er token

### Frontend
- Batching contenu streaming : 50 ms → 16 ms
- Durée affichée par consultation (timeline)
- Auto-scroll throttlé via `requestAnimationFrame`

## Mesurer le gain

```powershell
cd src\Backend\scripts
.\ai-chat-benchmark.ps1 -BearerToken "<jwt>" -Scenario all -Runs 5
```

Corréler avec les logs via `X-Trace-Id` :
- `phase=ai_tool_execute`
- `phase=llm_stream_round first_token_ms=...`

## Rollback

`appsettings.json` → section `Ollama` :
```json
"EnableParallelDbTools": false,
"EnableReadOnlyToolCache": false
```
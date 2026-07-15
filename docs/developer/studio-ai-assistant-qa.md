# Studio AI Assistant — QA checklist

## Feature flags

- `Ollama:EnableMutationTools` — must be `true` for table/system generation (dev default).
- `Ollama:EnableStudioAiTools` — master switch for `studio_*` tools.
- `Ollama:EnableStudioSystemGeneration` — multi-table `studio_generate_system` (set `false` in prod until validated).

## Smoke tests

1. `/studio/ai` — prompt without "creer": single table is created.
2. Multi-table prompt: system with relations + seed data.
3. Sidebar shows grouped system with child tables.
4. `/studio/systems/{key}` hub page loads.
5. General `/ai-assistant` unchanged.

## Migration

Apply tenant migration `20260624181553_AddStudioSystems_Tenant`.
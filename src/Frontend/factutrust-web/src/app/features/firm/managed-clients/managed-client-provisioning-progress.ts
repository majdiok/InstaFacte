/**
 * Estimated UX progress for firm-managed client provisioning.
 * Percentages are honest estimates (capped at 92 until HTTP completes) — not live server %.
 */

export type ProvisioningPhaseId =
  | 'validating'
  | 'creatingDatabase'
  | 'installingChart'
  | 'fiscalParams'
  | 'finalizing'
  | 'done'
  | 'error';

export type ProvisioningStepStatus = 'pending' | 'running' | 'done' | 'error';

export interface ProvisioningStep {
  id: Exclude<ProvisioningPhaseId, 'done' | 'error'>;
  label: string;
  status: ProvisioningStepStatus;
}

export interface ProvisioningSchedule {
  percent: number;
  activePhaseId: Exclude<ProvisioningPhaseId, 'done' | 'error'>;
  label: string;
}

/** Cap while the HTTP request is still in flight. */
export const PROVISIONING_PERCENT_CAP = 92;

const PHASE_DEFS: ReadonlyArray<{
  id: ProvisioningStep['id'];
  label: string;
  startSec: number;
  endSec: number;
  startPercent: number;
  endPercent: number;
}> = [
  {
    id: 'validating',
    label: 'Validation et enregistrement du dossier…',
    startSec: 0,
    endSec: 3,
    startPercent: 0,
    endPercent: 8
  },
  {
    id: 'creatingDatabase',
    label: 'Création de la base comptable…',
    startSec: 3,
    endSec: 8,
    startPercent: 8,
    endPercent: 20
  },
  {
    id: 'installingChart',
    label: 'Installation du plan comptable et des journaux…',
    startSec: 8,
    endSec: 45,
    startPercent: 20,
    endPercent: 75
  },
  {
    id: 'fiscalParams',
    label: 'Paramètres fiscaux et sociaux…',
    startSec: 45,
    endSec: 55,
    startPercent: 75,
    endPercent: 85
  },
  {
    id: 'finalizing',
    label: 'Finalisation (affectation et dossier permanent)…',
    startSec: 55,
    endSec: Number.POSITIVE_INFINITY,
    startPercent: 85,
    endPercent: PROVISIONING_PERCENT_CAP
  }
];

export function createInitialProvisioningSteps(): ProvisioningStep[] {
  return PHASE_DEFS.map((p, index) => ({
    id: p.id,
    label: p.label.replace(/…$/, ''),
    status: index === 0 ? 'running' : 'pending'
  }));
}

function lerp(start: number, end: number, t: number): number {
  return start + (end - start) * Math.min(1, Math.max(0, t));
}

/**
 * Maps elapsed seconds to an estimated percent (0–92) and active phase.
 * Installing-chart phase slows after 30s to avoid racing ahead of a long MigrateAsync.
 */
export function scheduleProvisioningProgress(elapsedSeconds: number): ProvisioningSchedule {
  const elapsed = Math.max(0, elapsedSeconds);

  for (const phase of PHASE_DEFS) {
    if (elapsed < phase.endSec || phase.endSec === Number.POSITIVE_INFINITY) {
      if (elapsed < phase.startSec) {
        continue;
      }

      let percent: number;
      if (phase.id === 'installingChart') {
        // 8–30s: 20→65 ; 30–45s: 65→75 (slower)
        if (elapsed < 30) {
          percent = lerp(20, 65, (elapsed - 8) / (30 - 8));
        } else if (elapsed < 45) {
          percent = lerp(65, 75, (elapsed - 30) / (45 - 30));
        } else {
          percent = 75;
        }
      } else if (phase.endSec === Number.POSITIVE_INFINITY) {
        // Asymptotic approach to cap after 55s
        const over = elapsed - phase.startSec;
        percent = Math.min(
          PROVISIONING_PERCENT_CAP,
          phase.startPercent + (PROVISIONING_PERCENT_CAP - phase.startPercent) * (1 - Math.exp(-over / 20))
        );
      } else {
        const span = phase.endSec - phase.startSec;
        percent = lerp(phase.startPercent, phase.endPercent, span > 0 ? (elapsed - phase.startSec) / span : 1);
      }

      return {
        percent: Math.min(PROVISIONING_PERCENT_CAP, Math.round(percent)),
        activePhaseId: phase.id,
        label: phase.label
      };
    }
  }

  const last = PHASE_DEFS[PHASE_DEFS.length - 1];
  return {
    percent: PROVISIONING_PERCENT_CAP,
    activePhaseId: last.id,
    label: last.label
  };
}

/** Rebuild step statuses from the active phase while running. */
export function applyRunningSchedule(
  steps: ProvisioningStep[],
  schedule: ProvisioningSchedule
): ProvisioningStep[] {
  const activeIndex = steps.findIndex(s => s.id === schedule.activePhaseId);
  return steps.map((step, index) => {
    if (activeIndex < 0) return step;
    if (index < activeIndex) return { ...step, status: 'done' as const };
    if (index === activeIndex) return { ...step, status: 'running' as const };
    return { ...step, status: 'pending' as const };
  });
}

/** Snap to 100% and mark all steps done. */
export function completeProvisioningSteps(steps: ProvisioningStep[]): ProvisioningStep[] {
  return steps.map(s => ({ ...s, status: 'done' as const }));
}

/** Freeze progress and mark the current running step as error. */
export function failProvisioningSteps(steps: ProvisioningStep[]): ProvisioningStep[] {
  return steps.map(s => {
    if (s.status === 'running') return { ...s, status: 'error' as const };
    return s;
  });
}

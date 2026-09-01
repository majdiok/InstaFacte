import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputSwitchModule } from 'primeng/inputswitch';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TabsModule } from 'primeng/tabs';
import { PayrollService, PayrollParameters, PayrollGarnishmentBracket, PayrollFeatureFlags, PayrollLegalPreset, PayrollAccountingSettings } from '@core/services/payroll.service';
import { ToastService } from '@core/services/toast.service';
import { ConfirmationService } from '@core/services/confirmation.service';
import { TooltipModule } from 'primeng/tooltip';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { ButtonComponent } from '@shared/components/button/button.component';
import { FormSectionComponent } from '@shared/components/form-section/form-section.component';
import { PayrollSocialFundsSettingsComponent } from './payroll-social-funds-settings.component';

@Component({
  selector: 'app-payroll-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabsModule,
    SelectModule,
    InputNumberModule,
    InputSwitchModule,
    TableModule,
    ButtonModule,
    PageHeaderComponent,
    ButtonComponent,
    FormSectionComponent,
    TooltipModule,
    PayrollSocialFundsSettingsComponent
  ],
  template: `
    <app-page-header title="Paramètres de paie" subtitle="Barème IRPP, taux légaux et conformité par exercice." />

    <div class="payroll-toolbar mb-3">
      <label for="fiscalYear">Exercice</label>
      <p-inputNumber id="fiscalYear" [(ngModel)]="fiscalYear" (ngModelChange)="load()" [useGrouping]="false" [min]="2000" [max]="2100" styleClass="w-8rem" />
      <app-button type="button" variant="secondary" label="Recharger défauts LF" icon="pi pi-refresh" (clicked)="reloadLegalPreset()" class="ml-3" />
    </div>

    @if (accountingForm(); as acc) {
      <div class="sce-config-card mb-3">
        <div class="sce-config-card__title">
          <i class="pi pi-calculator"></i>
          Comptabilisation de la paie — imputation comptable du dossier
        </div>
        <div class="sce-config-card__grid">
          <div>
            <label class="sce-config-card__label" for="sceProfile">Profil de comptabilisation</label>
            <p-select
              inputId="sceProfile"
              [options]="accountProfileOptions"
              [(ngModel)]="acc.accountProfile"
              [ngModelOptions]="{ standalone: true }"
              (ngModelChange)="onProfileChange($event)"
              optionLabel="label"
              optionValue="value"
              appendTo="body"
              styleClass="w-full" />
          </div>
          <div>
            <label class="sce-config-card__label" for="sceEffectiveDate">Date de bascule (1er du mois)</label>
            <input
              id="sceEffectiveDate"
              type="month"
              class="form-control"
              [ngModel]="effectiveMonth()"
              [ngModelOptions]="{ standalone: true }"
              (ngModelChange)="onEffectiveMonthChange($event)"
              [min]="earliestMonth()" />
            <small class="sce-config-card__hint">
              @if (accountingSettings()?.lastSettledPeriod) {
                Dernier cycle arrêté : {{ accountingSettings()?.lastSettledPeriod }}.
              } @else {
                Aucun cycle arrêté à ce jour.
              }
            </small>
          </div>
          <div>
            <label class="sce-config-card__label" for="sceInKindAccount">Compte de compensation avantage en nature</label>
            <input
              id="sceInKindAccount"
              type="text"
              class="form-control"
              maxlength="20"
              [(ngModel)]="acc.inKindOffsetAccount"
              [ngModelOptions]="{ standalone: true }" />
          </div>
          <div>
            <span class="sce-config-card__label">Règlement strict (avances/prêts/saisies)</span>
            <span class="sce-config-card__value">{{ featureFlags()?.payrollStrictSettlementEnabled ? 'Activé' : 'Désactivé' }}</span>
          </div>
          <div>
            <label class="sce-config-card__label" for="sceDisbursement">Écritures de décaissement (avances/prêts)</label>
            <p-inputSwitch inputId="sceDisbursement" [(ngModel)]="acc.disbursementEntriesEnabled" [ngModelOptions]="{ standalone: true }" />
          </div>
          <div>
            <label class="sce-config-card__label" for="sceSalarySplit">Ventilation détaillée des salaires (640)</label>
            <p-inputSwitch inputId="sceSalarySplit" [(ngModel)]="acc.detailedSalarySplitEnabled" [ngModelOptions]="{ standalone: true }" />
          </div>
        </div>

        @if (acc.accountProfile === 'Sce2026' && !acc.accountProfileEffectiveDate) {
          <p class="sce-warn-panel mt-2" role="alert">
            <i class="pi pi-exclamation-triangle"></i>
            <span>
              Le profil SCE 2026 exige une date de bascule. Sans elle, il s'appliquerait aussi aux cycles
              antérieurs : rouvrir puis revalider l'un d'eux en réécrirait l'imputation.
            </span>
          </p>
        }

        <p class="sce-config-card__note">
          @if (accountingSettings()?.isTenantOverride) {
            Réglage propre à ce dossier.
          } @else {
            Ce dossier hérite de la configuration globale ; enregistrer créera son réglage propre.
          }
          Les cycles antérieurs à la date de bascule conservent leur imputation d'origine.
          Les comptes SCE par régime de fonds social (part salarié / part employeur) sont configurés ci-dessous.
        </p>
        <div class="sce-config-card__actions">
          <app-button
            type="button"
            variant="primary"
            label="Enregistrer l'imputation"
            icon="pi pi-save"
            [disabled]="savingAccounting()"
            (clicked)="saveAccountingSettings()" />
        </div>
      </div>
    }

    @if (presetDrift().length > 0) {
      <div class="preset-drift-banner mb-3" role="alert" aria-live="polite">
        <div class="preset-drift-banner__title">
          <i class="pi pi-exclamation-triangle"></i>
          Écart preset légal — les paramètres de l'exercice {{ fiscalYear }} divergent du défaut LF
        </div>
        <ul class="preset-drift-banner__list">
          @for (d of presetDrift(); track d.label) {
            <li><strong>{{ d.label }}</strong> : tenant {{ d.current }} → LF {{ d.expected }}</li>
          }
        </ul>
        <div class="preset-drift-banner__actions">
          <app-button type="button" variant="secondary" label="Recharger défauts LF" icon="pi pi-refresh" (clicked)="reloadLegalPreset()" />
        </div>
      </div>
    }

    @if (params()) {
      <form (ngSubmit)="save()">
        <p-tabs class="ft-tabs" [lazy]="true">
          <p-tablist>
            <p-tab [value]="0"><i class="pi pi-percentage mr-2"></i><span>Taux légaux</span></p-tab>
            <p-tab [value]="1"><i class="pi pi-table mr-2"></i><span>Barème IRPP</span></p-tab>
            <p-tab [value]="2"><i class="pi pi-users mr-2"></i><span>Déductions familiales</span></p-tab>
            <p-tab [value]="3"><i class="pi pi-shield mr-2"></i><span>Conformité</span></p-tab>
            <p-tab [value]="4"><i class="pi pi-exclamation-triangle mr-2"></i><span>Saisies</span></p-tab>
            <p-tab [value]="5"><i class="pi pi-heart mr-2"></i><span>Mutuelles</span></p-tab>
          </p-tablist>
          <p-tabpanels>
          <p-tabpanel [value]="0">
            <app-form-section title="CNSS" icon="pi-building" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-4">
                <div class="payroll-form-group">
                  <label>CNSS salarié (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.cnssEmployeeRate" name="cnssEmployeeRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>CNSS employeur (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.cnssEmployerRate" name="cnssEmployerRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>CNSS salarié RSA (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.cnssEmployeeRateRsa" name="cnssEmployeeRateRsa" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>CNSS employeur RSA (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.cnssEmployerRateRsa" name="cnssEmployerRateRsa" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <app-form-section title="CSS" icon="pi-percentage" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-3">
                <div class="payroll-form-group">
                  <label>CSS (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.cssRate" name="cssRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label pTooltip="Seuil d'exonération CSS annuel">Seuil exonération CSS (TND/an)</label>
                  <p-inputNumber [(ngModel)]="params()!.cssAnnualExemptionThreshold" name="cssExemption" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>CSS patronale (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.cssEmployerRate" name="cssEmployerRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <app-form-section title="TFP &amp; FOPROLOS" icon="pi-briefcase" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-3">
                <div class="payroll-form-group">
                  <label>TFP industrie (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.tfpRateIndustry" name="tfpIndustry" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>TFP autres (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.tfpRateOther" name="tfpOther" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>FOPROLOS (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.foprolosRate" name="foprolos" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <app-form-section title="SMIG &amp; avantages" icon="pi-wallet" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-2">
                <div class="payroll-form-group">
                  <label>SMIG mensuel (TND)</label>
                  <p-inputNumber [(ngModel)]="params()!.monthlySmig" name="smig" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label pTooltip="Plafond exonération ticket restaurant / jour (TND)">Plafond ticket resto. / jour (TND)</label>
                  <p-inputNumber [(ngModel)]="params()!.mealVoucherDailyExemptionCap" name="mealVoucherCap" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <app-form-section title="Frais professionnels" icon="pi-calculator" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-2">
                <div class="payroll-form-group">
                  <label>Frais pro. (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.professionalExpensesRate" name="proExpRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>Plafond frais pro. (annuel)</label>
                  <p-inputNumber [(ngModel)]="params()!.professionalExpensesAnnualCap" name="proExpCap" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <p class="payroll-info-panel">Références légales tunisiennes : CNSS 9,18 % / 16,57 % — CSS 0,5 % (seuil 5 000 TND/an) — TFP 1 % industrie, 2 % autres — FOPROLOS 1 % — frais professionnels 10 % plafonnés à 2 000 TND/an.</p>
          </p-tabpanel>

          <p-tabpanel [value]="1">
            <p-table [value]="params()!.irppBrackets" styleClass="p-datatable-sm mb-3">
              <ng-template pTemplate="header">
                <tr>
                  <th>Seuil inférieur (TND)</th>
                  <th>Taux (%)</th>
                  <th></th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-bracket let-i="rowIndex">
                <tr>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.lowerBound" [name]="'lb' + i" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.rate" [name]="'rate' + i" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" (click)="removeBracket(i)"></button>
                  </td>
                </tr>
              </ng-template>
            </p-table>
            <app-button type="button" variant="outline" icon="pi-plus" iconPos="left" (click)="addBracket()">Ajouter une tranche</app-button>
            <p class="payroll-info-text mt-2">Barème LF 2025 : 0 % jusqu'à 5 000 — 15 % — 25 % — 30 % — 33 % — 36 % — 38 % — 40 % au-delà de 70 000 TND/an. La première tranche doit démarrer à 0 et les seuils être strictement croissants.</p>
          </p-tabpanel>

          <p-tabpanel [value]="2">
            <app-form-section title="Déductions de base" icon="pi-home" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-3">
                <div class="payroll-form-group">
                  <label>Chef de famille (annuel, TND)</label>
                  <p-inputNumber [(ngModel)]="params()!.headOfFamilyAnnualDeduction" name="headDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>Enfant à charge (annuel, TND)</label>
                  <p-inputNumber [(ngModel)]="params()!.childAnnualDeduction" name="childDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>Enfants déductibles max.</label>
                  <p-inputNumber [(ngModel)]="params()!.maxDeductibleChildren" name="maxChildren" [min]="0" [useGrouping]="false" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <app-form-section title="Enfants à cas particulier" icon="pi-user-plus" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-2">
                <div class="payroll-form-group">
                  <label pTooltip="Non boursier, âge &lt; 25 ans">Enfant étudiant (annuel, TND)</label>
                  <p-inputNumber [(ngModel)]="params()!.studentChildAnnualDeduction" name="studentDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label pTooltip="Hors plafond du nombre d'enfants déductibles">Enfant infirme (annuel, TND)</label>
                  <p-inputNumber [(ngModel)]="params()!.disabledChildAnnualDeduction" name="disabledDeduction" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <app-form-section title="Parents à charge" icon="pi-users" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-2">
                <div class="payroll-form-group">
                  <label pTooltip="Pourcentage du revenu net imposable">Parent à charge — taux (%)</label>
                  <p-inputNumber [(ngModel)]="params()!.parentDeductionRatePercent" name="parentRate" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
                <div class="payroll-form-group">
                  <label>Parent à charge — plafond annuel (TND)</label>
                  <p-inputNumber [(ngModel)]="params()!.parentAnnualDeductionCap" name="parentCap" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                </div>
              </div>
            </app-form-section>

            <p class="payroll-info-panel">Déductions familiales annuelles (art. 40 code IRPP), mensualisées lors du calcul de paie. Les montants s'appliquent par exercice fiscal.</p>
          </p-tabpanel>

          <p-tabpanel [value]="3">
            <app-form-section title="Options de calcul" icon="pi-cog" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-2">
                <div class="payroll-form-group switch-row">
                  <label for="enforceSmig">Contrôle SMIG sur les contrats</label>
                  <p-inputSwitch inputId="enforceSmig" [(ngModel)]="params()!.enforceSmigOnContracts" name="enforceSmig" />
                </div>
                <div class="payroll-form-group switch-row">
                  <label for="extendedOvertime">Taux HS étendus (175 %, 200 %)</label>
                  <p-inputSwitch inputId="extendedOvertime" [(ngModel)]="params()!.enableExtendedOvertimeRates" name="extendedOvertime" />
                </div>
                <div class="payroll-form-group switch-row">
                  <label for="allowanceMatrix">Matrice primes (imposable × CNSS)</label>
                  <p-inputSwitch inputId="allowanceMatrix" [(ngModel)]="params()!.enableAllowanceQuadrantMatrix" name="allowanceMatrix" />
                </div>
                <div class="payroll-form-group switch-row">
                  <label for="industrialSector">Secteur industriel (TFP 1 % au lieu de 2 %)</label>
                  <p-inputSwitch inputId="industrialSector" [(ngModel)]="params()!.isIndustrialSector" name="industrialSector" />
                </div>
                <div class="payroll-form-group switch-row">
                  <label for="irppRegularization">Régularisation IRPP annuelle (solde de tout compte)</label>
                  <p-inputSwitch inputId="irppRegularization" [(ngModel)]="params()!.enableIrppRegularization" name="irppRegularization" />
                </div>
                <div class="payroll-form-group switch-row">
                  <label for="automaticProrata">Prorata automatique (absences, suspensions, départs)</label>
                  <p-inputSwitch inputId="automaticProrata" [(ngModel)]="params()!.enableAutomaticProrata" name="automaticProrata" />
                </div>
              </div>
            </app-form-section>

            <app-form-section title="Exonération IRPP SMIG" icon="pi-shield" variant="compact">
              <div class="payroll-form-row payroll-form-row--cols-2">
                <div class="payroll-form-group">
                  <label for="smigExemptionMode">Exonération IRPP SMIG</label>
                  <p-select
                    inputId="smigExemptionMode"
                    [(ngModel)]="params()!.smigIrppExemptionMode"
                    name="smigExemptionMode"
                    [options]="smigExemptionModeOptions"
                    optionLabel="label"
                    optionValue="value"
                    styleClass="w-full" />
                </div>
                @if (params()!.smigIrppExemptionMode === 'SmigPortion') {
                  <div class="payroll-form-group">
                    <label>Taux applicable à la portion SMIG (%)</label>
                    <p-inputNumber
                      [(ngModel)]="params()!.smigIrppExemptionRateOverride"
                      name="smigExemptionRate"
                      [minFractionDigits]="2"
                      [maxFractionDigits]="2"
                      [min]="0"
                      [max]="100"
                      [locale]="'fr-TN'"
                      placeholder="15 (barème)"
                      styleClass="w-full" />
                    <small class="field-hint">Laisser vide pour utiliser le premier taux non nul du barème IRPP.</small>
                  </div>
                }
              </div>
              @if (params()!.smigIrppExemptionMode === 'SmigPortion') {
                <p class="sce-warn-panel" role="alert">
                  <i class="pi pi-exclamation-triangle"></i>
                  Ce mode n'a pas de base légale vérifiée et sous-déclare l'IRPP pour l'ensemble des salariés
                  lorsqu'il est actif. Pour la conformité, préférez le mode « Déduction annuelle 500 TND (SMIG/SMAG) »
                  qui applique une déduction annuelle supplémentaire de 500 TND à l'assiette imposable des salariés
                  payés au SMIG/SMAG.
                </p>
              }
            </app-form-section>

            <p class="payroll-info-panel">
              La déduction annuelle de 500 TND (mode conforme) s'applique à l'assiette imposable des salariés payés
              au SMIG/SMAG et s'intègre naturellement à la projection mensuelle et à la régularisation IRPP.
              Le mode « Portion SMIG exonérée » est conservé pour les tenants qui l'utilisent mais n'a pas de base
              légale vérifiée ; le mode « Exonération totale » ne concerne que les salaires de base ≤ SMIG.
              La CSS n'est pas impactée.
            </p>
            <p class="payroll-info-panel">Le taux TFP appliqué aux cycles de paie de cet exercice suit ce paramètre ; recalculez les cycles en brouillon pour l'appliquer.</p>
            <p class="payroll-info-panel">
              La régularisation IRPP calcule l'écart annuel IRPP/CSS lors d'un départ ou en fin d'exercice.
              Le prorata automatique réduit le brut des jours non travaillés (congés sans solde, suspensions non rémunérées, sortie en cours de mois).
              Les deux options s'appliquent aux cycles recalculés après modification.
            </p>
          </p-tabpanel>

          <p-tabpanel [value]="4">
            <p class="payroll-info-text mb-3">Barème de quotité saisissable sur le net mensuel (fraction saisissable par tranche). Si vide, 33 % du net s'applique par défaut.</p>
            <p-table [value]="garnishmentBrackets()" styleClass="p-datatable-sm mb-3">
              <ng-template pTemplate="header">
                <tr>
                  <th>Seuil net mensuel (TND)</th>
                  <th>Fraction saisissable (%)</th>
                  <th></th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-bracket let-i="rowIndex">
                <tr>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.lowerBoundMonthlyNet" [name]="'gbLb' + i" [minFractionDigits]="3" [min]="0" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <p-inputNumber [(ngModel)]="bracket.seizableFractionPercent" [name]="'gbFrac' + i" [minFractionDigits]="2" [maxFractionDigits]="4" [min]="0" [max]="100" [locale]="'fr-TN'" styleClass="w-full" />
                  </td>
                  <td>
                    <button type="button" pButton icon="pi pi-trash" class="p-button-text p-button-danger p-button-sm" (click)="removeGarnishmentBracket(i)"></button>
                  </td>
                </tr>
              </ng-template>
            </p-table>
            <app-button type="button" variant="outline" icon="pi-plus" iconPos="left" (click)="addGarnishmentBracket()">Ajouter une tranche</app-button>
            <div class="form-actions mt-4">
              <app-button type="button" variant="primary" icon="pi-check" iconPos="left" (click)="saveGarnishmentBrackets()">Enregistrer le barème saisies</app-button>
            </div>
          </p-tabpanel>

          <p-tabpanel [value]="5">
            <app-payroll-social-funds-settings [fiscalYear]="fiscalYear" />
          </p-tabpanel>
          </p-tabpanels>
        </p-tabs>

        <div class="form-actions mt-4">
          <app-button type="submit" variant="primary" icon="pi-check" iconPos="left">Enregistrer</app-button>
        </div>
      </form>
    }
  `,
  styles: [`
    .mb-3 { margin-bottom: var(--spacing-4); }
    .mt-2 { margin-top: var(--spacing-2); }
    .mt-4 { margin-top: var(--spacing-6); }
    .mr-2 { margin-right: var(--spacing-2); }
    .w-full { width: 100%; }
    .w-8rem { width: 8rem; }
    .form-actions { display: flex; gap: var(--spacing-3); }
    .sce-config-card { background: var(--surface-ground, #f8fafc); border: 1px solid var(--surface-border, #e2e8f0); border-radius: 8px; padding: var(--spacing-3); }
    .sce-config-card__title { display: flex; align-items: center; gap: var(--spacing-2); font-weight: 600; margin-bottom: var(--spacing-2); }
    .sce-config-card__title i { color: var(--primary-color, #2563eb); }
    .sce-config-card__grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr)); gap: var(--spacing-2) var(--spacing-4); }
    .sce-config-card__label { display: block; font-size: .75rem; color: var(--text-color-secondary, #64748b); }
    .sce-config-card__value { display: block; font-weight: 600; }
    .sce-config-card__note { margin: var(--spacing-2) 0 0; font-size: .8rem; color: var(--text-color-secondary, #64748b); }
    .sce-config-card__hint { display: block; margin-top: 2px; font-size: .72rem; color: var(--text-color-secondary, #64748b); }
    .sce-config-card__actions { display: flex; justify-content: flex-end; margin-top: var(--spacing-2); }
    .sce-warn-panel {
      display: flex; gap: var(--spacing-2); align-items: flex-start;
      margin: var(--spacing-2) 0 0; padding: var(--spacing-2) var(--spacing-3);
      background: var(--color-warning-50, #fffbeb); border: 1px solid var(--color-warning-300, #fcd34d);
      border-radius: var(--radius-md, 0.5rem); font-size: .85rem; color: var(--color-warning-800, #92400e);
    }
    .sce-warn-panel i { color: var(--color-warning-600, #d97706); flex-shrink: 0; margin-top: 2px; }
    .preset-drift-banner {
      background: var(--color-warning-50, #fffbeb); border: 1px solid var(--color-warning-300, #fcd34d);
      border-radius: 8px; padding: var(--spacing-3);
    }
    .preset-drift-banner__title { display: flex; align-items: center; gap: var(--spacing-2); font-weight: 600; color: var(--color-warning-800, #92400e); margin-bottom: var(--spacing-2); }
    .preset-drift-banner__title i { color: var(--color-warning-600, #d97706); }
    .preset-drift-banner__list { margin: 0 0 var(--spacing-2); padding-left: var(--spacing-4); font-size: .85rem; color: var(--color-warning-900, #78350f); }
    .preset-drift-banner__actions { display: flex; justify-content: flex-end; }
  `]
})
export class PayrollSettingsComponent implements OnInit {
  private readonly payroll = inject(PayrollService);
  private readonly toast = inject(ToastService);
  private readonly confirmation = inject(ConfirmationService);
  fiscalYear = new Date().getFullYear();
  params = signal<PayrollParameters | null>(null);
  garnishmentBrackets = signal<GarnishmentBracketFormRow[]>([]);
  /** Drapeaux paie du dossier (dont le réglage d'imputation effectivement appliqué). */
  featureFlags = signal<PayrollFeatureFlags | null>(null);
  /** Réglage d'imputation renvoyé par le serveur (référence pour les bornes et l'état d'origine). */
  accountingSettings = signal<PayrollAccountingSettings | null>(null);
  /** Copie éditable liée au formulaire. */
  accountingForm = signal<PayrollAccountingSettings | null>(null);
  savingAccounting = signal(false);

  readonly accountProfileOptions = [
    { label: 'Legacy (historique)', value: 'Legacy' },
    { label: 'SCE 2026 (comptes NCT 01)', value: 'Sce2026' }
  ];

  /** Date de bascule au format `yyyy-MM` attendu par l'input `type="month"`. */
  effectiveMonth = computed(() => {
    const value = this.accountingForm()?.accountProfileEffectiveDate;
    return value ? value.substring(0, 7) : '';
  });

  /** Borne basse proposée par le serveur : le mois suivant le dernier cycle arrêté. */
  earliestMonth = computed(() => {
    const value = this.accountingSettings()?.earliestEffectiveDate;
    return value ? value.substring(0, 7) : '';
  });
  /** Preset légal officiel de l'exercice (pour la bannière de dérive preset vs tenant). */
  legalPreset = signal<PayrollLegalPreset | null>(null);
  readonly smigExemptionModeOptions = [
    { label: 'Désactivée', value: 'None' },
    { label: 'Déduction annuelle 500 TND (SMIG/SMAG)', value: 'SmigAnnualDeduction' },
    { label: 'Portion SMIG exonérée', value: 'SmigPortion' },
    { label: 'Exonération totale si salaire ≤ SMIG', value: 'FullIfBelow' }
  ];

  /** Écarts entre les paramètres persistés du tenant et le preset légal de l'exercice (plan §4 WS-3). */
  presetDrift = computed<PresetDriftItem[]>(() => {
    const p = this.params();
    const preset = this.legalPreset();
    if (!p || !preset) return [];
    const drift: PresetDriftItem[] = [];
    const rate = (label: string, current: number, expected: number, unit = '%') => {
      if (Math.abs(current - expected) > 0.0001) {
        drift.push({ label, current: `${current.toFixed(2)} ${unit}`, expected: `${expected.toFixed(2)} ${unit}` });
      }
    };
    rate('CNSS salarié', p.cnssEmployeeRate, preset.cnssEmployeeRate);
    rate('CNSS employeur', p.cnssEmployerRate, preset.cnssEmployerRate);
    rate('CSS', p.cssRate, preset.cssRate);
    rate('SMIG mensuel', p.monthlySmig, preset.monthlySmig, 'TND');
    const pb = [...preset.irppBrackets].sort((a, b) => a.lowerBound - b.lowerBound);
    const cb = [...p.irppBrackets].sort((a, b) => a.lowerBound - b.lowerBound);
    const bracketsDiffer = cb.length !== pb.length
      || cb.some((b, i) => Math.abs(b.lowerBound - pb[i].lowerBound) > 0.01 || Math.abs(b.rate - pb[i].rate) > 0.0001);
    if (bracketsDiffer) {
      drift.push({ label: 'Barème IRPP', current: `${cb.length} tranche(s)`, expected: `${pb.length} tranche(s) LF` });
    }
    return drift;
  });

  ngOnInit(): void {
    this.load();
    this.loadFeatureFlags();
  }

  private loadFeatureFlags(): void {
    this.payroll.getFeatureFlags().subscribe({
      next: res => this.featureFlags.set(res.data ?? null),
      error: () => this.featureFlags.set(null)
    });
    this.loadAccountingSettings();
  }

  private loadAccountingSettings(): void {
    this.payroll.getAccountingSettings().subscribe({
      next: res => {
        const data = res.data ?? null;
        this.accountingSettings.set(data);
        this.accountingForm.set(data ? { ...data } : null);
      },
      error: () => {
        this.accountingSettings.set(null);
        this.accountingForm.set(null);
      }
    });
  }

  /**
   * Passer à SCE 2026 sans date de bascule appliquerait le profil à tous les cycles, y compris
   * arrêtés. On prérenseigne donc la première date acceptable calculée par le serveur, ou le mois
   * prochain quand aucun cycle n'est encore arrêté.
   */
  onProfileChange(profile: string): void {
    const form = this.accountingForm();
    if (!form) return;

    if (profile === 'Sce2026' && !form.accountProfileEffectiveDate) {
      const earliest = this.accountingSettings()?.earliestEffectiveDate;
      const fallback = new Date();
      fallback.setDate(1);
      fallback.setMonth(fallback.getMonth() + 1);
      const suggested = earliest ?? `${fallback.getFullYear()}-${String(fallback.getMonth() + 1).padStart(2, '0')}-01`;
      this.accountingForm.set({ ...form, accountProfile: profile, accountProfileEffectiveDate: suggested });
      return;
    }

    this.accountingForm.set({ ...form, accountProfile: profile });
  }

  onEffectiveMonthChange(month: string): void {
    const form = this.accountingForm();
    if (!form) return;
    this.accountingForm.set({
      ...form,
      accountProfileEffectiveDate: month ? `${month}-01` : null
    });
  }

  saveAccountingSettings(): void {
    const form = this.accountingForm();
    if (!form) return;

    const wasLegacy = (this.accountingSettings()?.accountProfile ?? 'Legacy') !== 'Sce2026';
    const becomesSce = form.accountProfile === 'Sce2026';

    // La bascule engage toutes les OD de paie à venir : on la fait confirmer explicitement, en
    // rappelant que les cycles antérieurs à la date d'effet gardent leur imputation.
    if (wasLegacy && becomesSce) {
      const from = form.accountProfileEffectiveDate
        ? new Date(form.accountProfileEffectiveDate).toLocaleDateString('fr-TN')
        : '—';
      this.confirmation.confirm({
        header: 'Basculer en imputation SCE 2026',
        message:
          `À partir du ${from}, les écritures de paie utiliseront les comptes NCT 01 : TFP en 6611, `
          + 'FOPROLOS en 6612, taxes patronales en 437, indemnités de rupture en 64602. Les cycles '
          + 'antérieurs à cette date conservent leur imputation actuelle et ne sont pas réécrits. Continuer ?',
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Basculer',
        rejectLabel: 'Annuler',
        size: 'md',
        accept: () => this.submitAccountingSettings(form)
      });
      return;
    }

    this.submitAccountingSettings(form);
  }

  private submitAccountingSettings(form: PayrollAccountingSettings): void {
    this.savingAccounting.set(true);
    this.payroll.updateAccountingSettings({
      accountProfile: form.accountProfile,
      accountProfileEffectiveDate: form.accountProfileEffectiveDate ?? null,
      inKindOffsetAccount: form.inKindOffsetAccount?.trim() || null,
      disbursementEntriesEnabled: form.disbursementEntriesEnabled,
      detailedSalarySplitEnabled: form.detailedSalarySplitEnabled
    }).subscribe({
      next: res => {
        this.savingAccounting.set(false);
        const data = res.data ?? null;
        this.accountingSettings.set(data);
        this.accountingForm.set(data ? { ...data } : null);
        this.loadFeatureFlags();
        this.toast.add({
          severity: 'success',
          summary: 'Comptabilisation',
          detail: "Profil d'imputation comptable enregistré."
        });
      },
      error: err => {
        this.savingAccounting.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Comptabilisation',
          detail: err?.error?.message ?? "Profil d'imputation non enregistré."
        });
      }
    });
  }

  private loadLegalPreset(): void {
    this.payroll.getLegalPreset(this.fiscalYear).subscribe({
      next: res => this.legalPreset.set(res.data ?? null),
      error: () => this.legalPreset.set(null)
    });
  }

  load(): void {
    this.payroll.getParameters(this.fiscalYear).subscribe({
      next: res => {
        const data = res.data;
        if (data) {
          this.params.set({
            ...data,
            enableIrppRegularization: data.enableIrppRegularization ?? false,
            enableAutomaticProrata: data.enableAutomaticProrata ?? false,
            smigIrppExemptionMode: data.smigIrppExemptionMode ?? 'None',
            smigIrppExemptionModeDisplay: data.smigIrppExemptionModeDisplay ?? 'Désactivée',
            smigIrppExemptionRateOverride: data.smigIrppExemptionRateOverride ?? null,
            mealVoucherDailyExemptionCap: data.mealVoucherDailyExemptionCap ?? 0,
            irppBrackets: data.irppBrackets.map(b => ({ ...b }))
          });
        } else {
          this.params.set(null);
        }
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Paramètres', detail: 'Paramètres introuvables.' })
    });
    this.loadGarnishmentBrackets();
    this.loadLegalPreset();
  }

  private loadGarnishmentBrackets(): void {
    this.payroll.getGarnishmentBrackets(this.fiscalYear).subscribe({
      next: res => {
        const rows = (res.data ?? []).map(b => ({
          lowerBoundMonthlyNet: b.lowerBoundMonthlyNet,
          seizableFractionPercent: b.seizableFraction * 100
        }));
        this.garnishmentBrackets.set(rows);
      },
      error: () => this.garnishmentBrackets.set([])
    });
  }

  addGarnishmentBracket(): void {
    this.garnishmentBrackets.update(rows => [...rows, { lowerBoundMonthlyNet: 0, seizableFractionPercent: 0 }]);
  }

  removeGarnishmentBracket(index: number): void {
    this.garnishmentBrackets.update(rows => rows.filter((_, i) => i !== index));
  }

  saveGarnishmentBrackets(): void {
    const rows = this.garnishmentBrackets();
    const sorted = [...rows].sort((a, b) => a.lowerBoundMonthlyNet - b.lowerBoundMonthlyNet);
    if (sorted.some(b => b.seizableFractionPercent < 0 || b.seizableFractionPercent > 100)) {
      this.toast.add({ severity: 'error', summary: 'Barème saisies', detail: 'Les fractions doivent être entre 0 et 100 %.' });
      return;
    }
    const payload: PayrollGarnishmentBracket[] = sorted.map(b => ({
      lowerBoundMonthlyNet: b.lowerBoundMonthlyNet,
      seizableFraction: b.seizableFractionPercent / 100
    }));
    this.payroll.updateGarnishmentBrackets(this.fiscalYear, payload).subscribe({
      next: () => this.toast.add({ severity: 'success', summary: 'Barème saisies', detail: 'Barème enregistré.' }),
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Barème saisies',
        detail: err?.error?.message ?? 'Enregistrement impossible.'
      })
    });
  }

  addBracket(): void {
    const p = this.params();
    if (!p) return;
    p.irppBrackets.push({ lowerBound: 0, rate: 0 });
    this.params.set({ ...p, irppBrackets: [...p.irppBrackets] });
  }

  removeBracket(index: number): void {
    const p = this.params();
    if (!p) return;
    const brackets = p.irppBrackets.filter((_, i) => i !== index);
    this.params.set({ ...p, irppBrackets: brackets });
  }

  /** Contrôle client du barème IRPP ; renvoie null si valide, sinon le message d'erreur. */
  private validateBrackets(brackets: { lowerBound: number; rate: number }[]): string | null {
    if (!brackets.length) return 'Le barème IRPP doit comporter au moins une tranche.';
    const sorted = [...brackets].sort((a, b) => a.lowerBound - b.lowerBound);
    if (sorted[0].lowerBound !== 0) return 'La première tranche IRPP doit démarrer à 0.';
    for (let i = 1; i < sorted.length; i++) {
      if (sorted[i].lowerBound === sorted[i - 1].lowerBound)
        return 'Deux tranches IRPP ne peuvent pas avoir le même seuil inférieur.';
    }
    if (sorted.some(b => b.rate < 0 || b.rate > 100)) return 'Les taux IRPP doivent être compris entre 0 et 100 %.';
    return null;
  }

  reloadLegalPreset(): void {
    this.payroll.getLegalPreset(this.fiscalYear).subscribe({
      next: res => {
        const preset = res.data;
        const current = this.params();
        if (!preset || !current) return;
        this.params.set({
          ...current,
          cnssEmployeeRate: preset.cnssEmployeeRate,
          cnssEmployerRate: preset.cnssEmployerRate,
          cssRate: preset.cssRate,
          monthlySmig: preset.monthlySmig,
          irppBrackets: preset.irppBrackets.map(b => ({ ...b }))
        });
        this.toast.add({
          severity: 'info',
          summary: 'Preset LF',
          detail: `${preset.label} chargé — enregistrez pour appliquer.`
        });
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Preset LF', detail: 'Chargement impossible.' })
    });
  }

  save(): void {
    const p = this.params();
    if (!p) return;
    const bracketError = this.validateBrackets(p.irppBrackets);
    if (bracketError) {
      this.toast.add({ severity: 'error', summary: 'Barème IRPP', detail: bracketError });
      return;
    }
    const { id, fiscalYear, ...body } = p;
    this.payroll.updateParameters(this.fiscalYear, body).subscribe({
      next: () => this.toast.add({ severity: 'success', summary: 'Paramètres', detail: 'Paramètres enregistrés.' }),
      error: err => this.toast.add({
        severity: 'error',
        summary: 'Paramètres',
        detail: err?.error?.message ?? 'Enregistrement impossible.'
      })
    });
  }
}

interface GarnishmentBracketFormRow {
  lowerBoundMonthlyNet: number;
  seizableFractionPercent: number;
}

interface PresetDriftItem {
  label: string;
  current: string;
  expected: string;
}

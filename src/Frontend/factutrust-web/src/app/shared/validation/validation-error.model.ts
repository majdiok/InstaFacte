/**
 * Unified Validation Error Models
 * Synchronized with backend ValidationDtos.cs
 */

// ============================================
// ERROR CODES
// ============================================

export const ValidationErrorCodes = {
  // General
  VALIDATION_FAILED: 'VALIDATION_FAILED',
  COMPLIANCE_ERROR: 'COMPLIANCE_ERROR',
  SECURITY_ERROR: 'SECURITY_ERROR',
  
  // Field validation
  REQUIRED: 'REQUIRED',
  MIN_LENGTH: 'MIN_LENGTH',
  MAX_LENGTH: 'MAX_LENGTH',
  INVALID_FORMAT: 'INVALID_FORMAT',
  INVALID_ENUM: 'INVALID_ENUM',
  MIN_VALUE: 'MIN_VALUE',
  MAX_VALUE: 'MAX_VALUE',
  INVALID_DATE: 'INVALID_DATE',
  FUTURE_DATE: 'FUTURE_DATE',
  PAST_DATE: 'PAST_DATE',
  DATE_RANGE: 'DATE_RANGE',
  
  // Tunisian-specific
  INVALID_NIF: 'INVALID_NIF',
  INVALID_EMAIL: 'INVALID_EMAIL',
  INVALID_PHONE: 'INVALID_PHONE',
  INVALID_IBAN: 'INVALID_IBAN',
  INVALID_RIB: 'INVALID_RIB',
  INVALID_POSTAL_CODE: 'INVALID_POSTAL_CODE',
  INVALID_GOVERNORATE: 'INVALID_GOVERNORATE',
  INVALID_VAT_RATE: 'INVALID_VAT_RATE',
  
  // Business logic
  DUPLICATE: 'DUPLICATE',
  NOT_FOUND: 'NOT_FOUND',
  ALREADY_EXISTS: 'ALREADY_EXISTS',
  CALCULATION_MISMATCH: 'CALCULATION_MISMATCH',
  INSUFFICIENT_DATA: 'INSUFFICIENT_DATA',
  INVALID_STATE: 'INVALID_STATE',
  DISCOUNT_EXCEEDS_TOTAL: 'DISCOUNT_EXCEEDS_TOTAL',
  CREDIT_NOTE_EXCEEDS_INVOICE: 'CREDIT_NOTE_EXCEEDS_INVOICE',
  
  // Security
  TAMPERING: 'TAMPERING_DETECTED',
  IDEMPOTENCY_VIOLATION: 'IDEMPOTENCY_VIOLATION',
  RATE_LIMIT_EXCEEDED: 'RATE_LIMIT_EXCEEDED',
  UNAUTHORIZED: 'UNAUTHORIZED'
} as const;

export type ValidationErrorCode = typeof ValidationErrorCodes[keyof typeof ValidationErrorCodes];

// ============================================
// ERROR SEVERITY
// ============================================

export type ErrorSeverity = 'ERROR' | 'WARNING';

// ============================================
// FIELD ERROR
// ============================================

export interface FieldError {
  /** Normalized error code for programmatic handling. */
  code: ValidationErrorCode | string;
  /** Human-readable error message in French. */
  message: string;
  /** The rejected value (for debugging). */
  attemptedValue?: unknown;
  /** Error severity: ERROR (blocking) or WARNING (non-blocking). */
  severity: ErrorSeverity;
}

// ============================================
// VALIDATION ERROR RESPONSE
// ============================================

export interface ValidationErrorResponse {
  /** Always false for error responses. */
  success: false;
  /** Global error code. */
  code: string;
  /** Human-readable error message. */
  message: string;
  /** Field-specific errors (mappable to form fields). */
  fieldErrors: Record<string, FieldError[]>;
  /** Global errors not tied to a specific field. */
  globalErrors: string[];
  /** Wizard step with the first error (0-indexed). */
  wizardStep?: number;
  /** Can the operation proceed despite errors (warnings only)? */
  canProceed: boolean;
  /** Total blocking error count. */
  errorCount: number;
  /** Warning count (non-blocking). */
  warningCount: number;
}

// ============================================
// WIZARD STEP KEYS
// ============================================

export type WizardStepKey = 'metadata' | 'seller' | 'client' | 'lines' | 'legal' | 'preview';

// ============================================
// WIZARD FIELD ERRORS
// ============================================

export type WizardFieldErrors = Record<WizardStepKey, Record<string, FieldError[]>>;

// ============================================
// FIELD PATH TO STEP MAPPING
// ============================================

const FIELD_TO_STEP_MAP: Record<string, WizardStepKey> = {
  // Step 0: Metadata
  'metadata.type': 'metadata',
  'metadata.issueDate': 'metadata',
  'metadata.dueDate': 'metadata',
  'metadata.currency': 'metadata',
  'metadata.internalReference': 'metadata',
  'metadata.linkedInvoiceId': 'metadata',
  'type': 'metadata',
  'issueDate': 'metadata',
  'dueDate': 'metadata',
  'currency': 'metadata',
  'internalReference': 'metadata',
  'linkedInvoiceId': 'metadata',
  
  // Step 1: Seller
  'seller': 'seller',
  'sellerId': 'seller',
  'seller.nif': 'seller',
  'seller.companyName': 'seller',
  'seller.address': 'seller',
  
  // Step 2: Client
  'client': 'client',
  'clientId': 'client',
  'newClient': 'client',
  'client.name': 'client',
  'newClient.name': 'client',
  'client.nif': 'client',
  'newClient.nif': 'client',
  'client.taxType': 'client',
  'newClient.taxType': 'client',
  'client.email': 'client',
  'newClient.email': 'client',
  'client.phone': 'client',
  'newClient.phone': 'client',
  'client.address': 'client',
  'newClient.address': 'client',
  'client.address.street': 'client',
  'newClient.address.street': 'client',
  'client.address.city': 'client',
  'newClient.address.city': 'client',
  'client.address.governorate': 'client',
  'newClient.address.governorate': 'client',
  'client.address.postalCode': 'client',
  'newClient.address.postalCode': 'client',
  'name': 'client',
  'nif': 'client',
  'taxType': 'client',
  'email': 'client',
  'phone': 'client',
  'street': 'client',
  'city': 'client',
  'governorate': 'client',
  'postalCode': 'client',
  
  // Step 3: Lines
  'lines': 'lines',
  'lines.designation': 'lines',
  'lines.quantity': 'lines',
  'lines.unitPriceHT': 'lines',
  'lines.vatRate': 'lines',
  'lines.discountValue': 'lines',
  'lines.discountType': 'lines',
  'totals': 'lines',
  'designation': 'lines',
  'quantity': 'lines',
  'unitPriceHT': 'lines',
  'vatRate': 'lines',
  'discountValue': 'lines',
  'discountType': 'lines',
  
  // Step 4: Payment & Legal
  'paymentMethod': 'legal',
  'payment': 'legal',
  'payment.method': 'legal',
  'payment.daysUntilDue': 'legal',
  'bankInfo': 'legal',
  'bankInfo.rib': 'legal',
  'bankInfo.iban': 'legal',
  'legalMentions': 'legal',
  'legalMentions.vatMention': 'legal',
  'legalMentions.exemptionMention': 'legal',
  'paymentLegal': 'legal',
  'paymentLegal.paymentMethod': 'legal',
  'paymentLegal.bankInfo.rib': 'legal',
  'paymentLegal.bankInfo.iban': 'legal',
  'paymentLegal.legalMentions.vatMention': 'legal',
  'paymentLegal.legalMentions.exemptionMention': 'legal',
  'method': 'legal',
  'daysUntilDue': 'legal',
  'rib': 'legal',
  'iban': 'legal',
  'vatMention': 'legal',
  'exemptionMention': 'legal'
};

// ============================================
// HELPER FUNCTIONS
// ============================================

/**
 * Gets the wizard step for a field path.
 */
export function getStepForField(fieldPath: string): WizardStepKey | null {
  // Direct lookup
  const step = FIELD_TO_STEP_MAP[fieldPath];
  if (step) return step;
  
  // Try by prefix
  const prefix = fieldPath.split('.')[0];
  return FIELD_TO_STEP_MAP[prefix] || null;
}

/**
 * Gets the step index for a wizard step key.
 */
export function getStepIndex(stepKey: WizardStepKey): number {
  const stepOrder: WizardStepKey[] = ['metadata', 'seller', 'client', 'lines', 'legal', 'preview'];
  return stepOrder.indexOf(stepKey);
}

/**
 * Gets the first step with errors.
 */
export function getFirstErrorStep(fieldErrors: Record<string, FieldError[]>): number | null {
  let firstStep: number | null = null;
  
  for (const fieldPath of Object.keys(fieldErrors)) {
    const stepKey = getStepForField(fieldPath);
    if (stepKey) {
      const stepIndex = getStepIndex(stepKey);
      if (firstStep === null || stepIndex < firstStep) {
        firstStep = stepIndex;
      }
    }
  }
  
  return firstStep;
}

/**
 * Creates an empty WizardFieldErrors object.
 */
export function createEmptyWizardErrors(): WizardFieldErrors {
  return {
    metadata: {},
    seller: {},
    client: {},
    lines: {},
    legal: {},
    preview: {}
  };
}

/**
 * Checks if a response is a validation error response.
 */
export function isValidationErrorResponse(response: unknown): response is ValidationErrorResponse {
  return (
    typeof response === 'object' &&
    response !== null &&
    'success' in response &&
    (response as { success: boolean }).success === false &&
    'fieldErrors' in response
  );
}

/**
 * Extracts the first error message for a field.
 */
export function getFirstErrorMessage(errors: FieldError[] | undefined): string | null {
  if (!errors || errors.length === 0) return null;
  return errors[0].message;
}

/**
 * Checks if there are any blocking errors.
 */
export function hasBlockingErrors(errors: FieldError[]): boolean {
  return errors.some(e => e.severity === 'ERROR');
}

/**
 * Counts errors by severity.
 */
export function countBySeverity(fieldErrors: Record<string, FieldError[]>): { errors: number; warnings: number } {
  let errors = 0;
  let warnings = 0;
  
  for (const errorList of Object.values(fieldErrors)) {
    for (const error of errorList) {
      if (error.severity === 'ERROR') {
        errors++;
      } else {
        warnings++;
      }
    }
  }
  
  return { errors, warnings };
}

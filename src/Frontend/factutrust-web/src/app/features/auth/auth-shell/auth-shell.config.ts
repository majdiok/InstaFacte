export interface AuthShellFeature {
  icon: string;
  label: string;
  description?: string;
}

export type AuthShellBadgeVariant = 'secure' | 'new' | 'context' | 'firm';

export interface AuthShellConfig {
  badge: string;
  badgeVariant?: AuthShellBadgeVariant;
  title: string;
  description?: string;
  features?: AuthShellFeature[];
  showAiHighlight?: boolean;
  showTrustFooter?: boolean;
  /** Wider floating card for multi-step registration wizards. */
  wideFormCard?: boolean;
  /**
   * When true, the floating form card is height-capped and scrolls internally.
   * Default: true only for wide wizard cards. Compact pages (login, etc.) grow to fit.
   */
  scrollableFormCard?: boolean;
}

/** Full-viewport office photo used as decorative auth background. */
export const AUTH_HERO_OFFICE = 'assets/branding/auth-hero-office.webp';
export const AUTH_HERO_OFFICE_FALLBACK = 'assets/branding/auth-hero-office.jpg';

const LOGIN_FEATURES: AuthShellFeature[] = [
  {
    icon: 'pi-chart-bar',
    label: 'Tableaux de bord intelligents',
    description: 'Suivez vos indicateurs clés en temps réel et pilotez votre activité d’un coup d’œil.',
  },
  {
    icon: 'pi-shopping-cart',
    label: 'Ventes & Achats optimisés',
    description: 'Devis, commandes, factures et achats dans un flux unique.',
  },
  {
    icon: 'pi-box',
    label: 'Stocks & Inventaires maîtrisés',
    description: 'Suivi intelligent des stocks, alertes et inventaires simplifiés.',
  },
  {
    icon: 'pi-chart-pie',
    label: 'Analyses & Rapports avancés',
    description: 'Des rapports clairs, assistés par l’IA, pour décider plus vite.',
  },
];

const REGISTER_FEATURES: AuthShellFeature[] = [
  {
    icon: 'pi-file-edit',
    label: 'Facturation électronique conforme',
    description: 'Émettez et recevez vos factures dans le respect des normes en vigueur.',
  },
  {
    icon: 'pi-shopping-cart',
    label: 'Gestion commerciale intégrée',
    description: 'Centralisez ventes, achats et clients dans un seul espace.',
  },
  {
    icon: 'pi-box',
    label: 'Suivi des stocks en temps réel',
    description: 'Anticipez les ruptures et suivez vos mouvements d’inventaire.',
  },
  {
    icon: 'pi-chart-line',
    label: 'Pilotage financier simplifié',
    description: 'Visualisez votre activité et prenez des décisions plus rapidement.',
  },
];

const REGISTER_FIRM_FEATURES: AuthShellFeature[] = [
  {
    icon: 'pi-users',
    label: 'Gestion multi-dossiers clients',
    description: 'Accédez à tous vos dossiers sociétés depuis un espace cabinet unique.',
  },
  {
    icon: 'pi-briefcase',
    label: 'Pilotage cabinet centralisé',
    description: 'Suivez l’activité, les invitations et la collaboration d’équipe.',
  },
  {
    icon: 'pi-shield',
    label: 'Accès délégué sécurisé',
    description: 'Intervenez sur les dossiers clients sans compromettre la sécurité.',
  },
  {
    icon: 'pi-chart-bar',
    label: 'Rentabilité et reporting',
    description: 'Mesurez la performance de vos missions et de vos collaborateurs.',
  },
];

const WAREHOUSE_FEATURES: AuthShellFeature[] = [
  {
    icon: 'pi-box',
    label: 'Données de stock filtrées par site',
    description: 'Les stocks et opérations s’adaptent à l’entrepôt sélectionné.',
  },
  {
    icon: 'pi-sync',
    label: 'Contexte de travail mémorisé',
    description: 'Votre choix d’entrepôt est conservé pour cette session.',
  },
];

export const LOGIN_AUTH_SHELL_CONFIG: AuthShellConfig = {
  badge: 'Espace sécurisé',
  badgeVariant: 'secure',
  title: 'Pilotez votre activité commerciale en toute sérénité',
  description:
    'InstaFact est la solution intelligente tout-en-un pour gérer vos ventes, achats, stocks, clients et facturation électronique.',
  features: LOGIN_FEATURES,
  showAiHighlight: true,
  showTrustFooter: true,
  scrollableFormCard: false,
};

export const REGISTER_AUTH_SHELL_CONFIG: AuthShellConfig = {
  badge: 'Nouveau',
  badgeVariant: 'new',
  title: 'Simplifiez votre gestion dès aujourd\'hui',
  description:
    'Rejoignez InstaFact et prenez le contrôle de votre facturation et de vos flux financiers en quelques étapes.',
  features: REGISTER_FEATURES,
  showAiHighlight: true,
  showTrustFooter: true,
  wideFormCard: true,
  scrollableFormCard: false,
};

export const REGISTER_FIRM_AUTH_SHELL_CONFIG: AuthShellConfig = {
  badge: 'Cabinet comptable',
  badgeVariant: 'firm',
  title: 'Pilotez votre cabinet en toute sérénité',
  description:
    'Centralisez la gestion de vos dossiers clients, collaborez en équipe et offrez un service premium à vos entreprises.',
  features: REGISTER_FIRM_FEATURES,
  showAiHighlight: false,
  showTrustFooter: true,
  wideFormCard: true,
  scrollableFormCard: false,
};

export const SELECT_WAREHOUSE_AUTH_SHELL_CONFIG: AuthShellConfig = {
  badge: 'Contexte de travail',
  badgeVariant: 'context',
  title: 'Choisissez votre entrepôt',
  description:
    'Les données de stock et certaines opérations sont filtrées par entrepôt. Sélectionnez celui dans lequel vous travaillez.',
  features: WAREHOUSE_FEATURES,
  showAiHighlight: false,
  showTrustFooter: true,
};

export const FORGOT_PASSWORD_AUTH_SHELL_CONFIG: AuthShellConfig = {
  badge: 'Sécurisé',
  badgeVariant: 'secure',
  title: 'Récupérez l\'accès à votre espace',
  description:
    'Saisissez l\'adresse email associée à votre compte. Nous vous enverrons un lien pour réinitialiser votre mot de passe.',
  showAiHighlight: false,
  showTrustFooter: true,
};

export const RESET_PASSWORD_AUTH_SHELL_CONFIG: AuthShellConfig = {
  badge: 'Sécurisé',
  badgeVariant: 'secure',
  title: 'Définissez votre nouveau mot de passe',
  description:
    'Choisissez un mot de passe fort pour sécuriser votre compte InstaFact.',
  showAiHighlight: false,
  showTrustFooter: true,
};

export const PORTAL_INVITE_AUTH_SHELL_CONFIG: AuthShellConfig = {
  badge: 'Espace client',
  badgeVariant: 'secure',
  title: 'Activez votre espace client',
  description:
    'Choisissez un mot de passe pour consulter vos factures, paiements et relevé auprès de votre fournisseur.',
  showAiHighlight: false,
  showTrustFooter: true,
};

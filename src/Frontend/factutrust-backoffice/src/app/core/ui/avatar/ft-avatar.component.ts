import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type FtAvatarSize = 'xs' | 'sm' | 'md' | 'lg';

/**
 * Avatar avec initiales sur fond colorisé déterministe.
 *
 * - Si `imageUrl` est fourni, affiche l'image.
 * - Sinon, calcule les initiales (2 caractères) à partir de `name` en sautant
 *   les abréviations courantes (`SARL`, `SA`, `Sté`, `Ets`, `Cie`, `LLC`).
 * - La couleur de fond est dérivée d'un hash djb2 de `seed` (ou `name`) sur
 *   12 teintes HSL fixes, compatibles dark mode.
 *
 * Usage :
 *  ```html
 *  <ft-avatar [name]="row.companyName" [seed]="row.tenantId" size="sm" />
 *  ```
 */
@Component({
  selector: 'ft-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="avatar"
      [class.avatar--xs]="size() === 'xs'"
      [class.avatar--sm]="size() === 'sm'"
      [class.avatar--md]="size() === 'md'"
      [class.avatar--lg]="size() === 'lg'"
      [style.background]="hasImage() ? null : background()"
      [attr.aria-label]="name() ? 'Avatar de ' + name() : null"
      role="img"
    >
      @if (hasImage()) {
        <img [src]="imageUrl()" [alt]="name() ?? ''" loading="lazy" />
      } @else {
        <span class="initials">{{ initials() }}</span>
      }
    </span>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
      }

      .avatar {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        border-radius: var(--ft-radius-pill);
        color: white;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: -0.01em;
        flex-shrink: 0;
        overflow: hidden;
        line-height: 1;
        user-select: none;
      }

      .avatar--xs {
        width: 1.25rem;
        height: 1.25rem;
        font-size: 0.6rem;
      }
      .avatar--sm {
        width: 1.75rem;
        height: 1.75rem;
        font-size: 0.72rem;
      }
      .avatar--md {
        width: 2.25rem;
        height: 2.25rem;
        font-size: 0.85rem;
      }
      .avatar--lg {
        width: 3rem;
        height: 3rem;
        font-size: 1.05rem;
      }

      img {
        width: 100%;
        height: 100%;
        object-fit: cover;
        display: block;
      }
    `
  ]
})
export class FtAvatarComponent {
  readonly name = input<string | null>(null);
  readonly seed = input<string | null>(null);
  readonly imageUrl = input<string | null>(null);
  readonly size = input<FtAvatarSize>('md');

  // Palette HSL — 12 teintes ; saturation/luminosité tunées dark mode.
  private static readonly PALETTE_HUES = [200, 220, 245, 270, 290, 320, 345, 10, 30, 80, 130, 165];
  private static readonly STOP_WORDS = new Set([
    'sarl',
    'sa',
    'ste',
    'sté',
    'ets',
    'cie',
    'llc',
    'inc',
    'ltd',
    'gmbh',
    'spa',
    'co',
    'and',
    'et',
    'the',
    'le',
    'la',
    'les',
    'de',
    'du',
    'des'
  ]);

  readonly hasImage = computed(() => !!this.imageUrl());

  readonly initials = computed(() => {
    const name = this.name()?.trim();
    if (!name) {
      return '?';
    }
    const words = name
      .split(/[\s\-_·.]+/)
      .map((w) => w.toLowerCase())
      .filter((w) => w.length > 0 && !FtAvatarComponent.STOP_WORDS.has(w));
    if (words.length === 0) {
      return name.slice(0, 2).toUpperCase();
    }
    if (words.length === 1) {
      return words[0].slice(0, 2).toUpperCase();
    }
    return (words[0][0] + words[1][0]).toUpperCase();
  });

  readonly background = computed(() => {
    const seed = (this.seed() ?? this.name() ?? 'x').trim();
    const hue = FtAvatarComponent.PALETTE_HUES[FtAvatarComponent.djb2(seed) % FtAvatarComponent.PALETTE_HUES.length];
    return `linear-gradient(135deg, hsl(${hue} 55% 42%) 0%, hsl(${hue} 60% 32%) 100%)`;
  });

  /** Hash djb2 (32-bit positif). */
  private static djb2(str: string): number {
    let hash = 5381;
    for (let i = 0; i < str.length; i++) {
      hash = (hash * 33) ^ str.charCodeAt(i);
    }
    return hash >>> 0;
  }
}

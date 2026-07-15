import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PowerPointThemePickerComponent } from './powerpoint-theme-picker.component';
import {
  PowerPointTemplate,
  PowerPointTemplateInfo,
  PowerPointThemeCategory,
  PowerPointThemeEngine,
  CoverLayoutStyle
} from '../../models/ai-chat.models';

const sampleTemplates: PowerPointTemplateInfo[] = [
  {
    id: PowerPointTemplate.Standard,
    key: 'Standard',
    name: 'Standard',
    description: 'd',
    category: PowerPointThemeCategory.Light,
    isDark: false,
    sortOrder: 0,
    primaryColorHex: '#0F172A',
    accentColorHex: '#2563EB',
    backgroundColorHex: '#FFFFFF',
    surfaceColorHex: '#F8FAFC',
    onSurfaceColorHex: '#0F172A',
    linkColorHex: '#2563EB',
    titleFontFamily: 'Inter',
    bodyFontFamily: 'Inter',
    engine: PowerPointThemeEngine.Legacy,
    requiresAttribution: false,
    coverLayoutStyle: CoverLayoutStyle.ClassicBar
  },
  {
    id: PowerPointTemplate.Vortex,
    key: 'Vortex',
    name: 'Vortex',
    description: 'Thème sombre violet',
    category: PowerPointThemeCategory.Dark,
    isDark: true,
    sortOrder: 10,
    primaryColorHex: '#F5F3FF',
    accentColorHex: '#A78BFA',
    backgroundColorHex: '#1E1B4B',
    surfaceColorHex: '#312E81',
    onSurfaceColorHex: '#EDE9FE',
    linkColorHex: '#A78BFA',
    titleFontFamily: 'Inter',
    bodyFontFamily: 'Inter',
    previewThumbnailUrl: '/assets/powerpoint/themes/Vortex/preview-16x9.svg',
    engine: PowerPointThemeEngine.Hybrid,
    requiresAttribution: false,
    coverLayoutStyle: CoverLayoutStyle.ClassicBar
  }
];

describe('PowerPointThemePickerComponent', () => {
  let fixture: ComponentFixture<PowerPointThemePickerComponent>;
  let component: PowerPointThemePickerComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PowerPointThemePickerComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(PowerPointThemePickerComponent);
    component = fixture.componentInstance;
    component.templates = sampleTemplates;
    fixture.detectChanges();
  });

  it('renders all templates when filter is Tous', () => {
    const cards = fixture.nativeElement.querySelectorAll('.ppt-theme-card');
    expect(cards.length).toBe(2);
  });

  it('filters by dark category', () => {
    component['setFilter'](PowerPointThemeCategory.Dark);
    fixture.detectChanges();
    const cards = fixture.nativeElement.querySelectorAll('.ppt-theme-card');
    expect(cards.length).toBe(1);
    expect(cards[0].textContent).toContain('Vortex');
  });

  it('emits selectedChange when a card is clicked', () => {
    const spy = jasmine.createSpy('selectedChange');
    component.selectedChange.subscribe(spy);
    const cards = fixture.nativeElement.querySelectorAll('.ppt-theme-card');
    cards[1].click();
    expect(spy).toHaveBeenCalledWith(PowerPointTemplate.Vortex);
  });

  it('filters by search query', () => {
    component['searchQuery'].set('vortex');
    fixture.detectChanges();
    const cards = fixture.nativeElement.querySelectorAll('.ppt-theme-card');
    expect(cards.length).toBe(1);
    expect(cards[0].textContent).toContain('Vortex');
  });

  it('renders thumbnail image when previewThumbnailUrl is set', () => {
    const img = fixture.nativeElement.querySelector('.ppt-theme-thumb') as HTMLImageElement;
    expect(img).toBeTruthy();
    expect(img.src).toContain('preview-16x9.svg');
  });

  it('falls back to CSS preview when thumbnail image fails to load', () => {
    const img = fixture.nativeElement.querySelector('.ppt-theme-thumb') as HTMLImageElement;
    expect(img).toBeTruthy();
    img.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.ppt-theme-thumb')).toBeNull();
    expect(fixture.nativeElement.querySelector('.ppt-theme-preview-fallback')).toBeTruthy();
  });

  it('renders CSS fallback when previewThumbnailUrl is absent', () => {
    const legacyFixture = TestBed.createComponent(PowerPointThemePickerComponent);
    legacyFixture.componentInstance.templates = [sampleTemplates[0]];
    legacyFixture.detectChanges();

    expect(legacyFixture.nativeElement.querySelectorAll('.ppt-theme-card').length).toBe(1);
    expect(legacyFixture.nativeElement.querySelector('.ppt-theme-thumb')).toBeNull();
    expect(legacyFixture.nativeElement.querySelector('.ppt-theme-preview-fallback')).toBeTruthy();
  });
});

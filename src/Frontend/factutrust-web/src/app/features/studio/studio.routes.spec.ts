import { Route } from '@angular/router';
import { StudioShellComponent } from './shared/studio-shell.component';
import { STUDIO_CHILD_ROUTES, STUDIO_ROUTES } from './studio.routes';

describe('STUDIO_ROUTES', () => {
  const shell = STUDIO_ROUTES[0];
  const child = (path: string): Route => {
    const found = STUDIO_CHILD_ROUTES.find(r => r.path === path);
    if (!found) throw new Error(`route ${path} introuvable`);
    return found;
  };

  it('enveloppe toutes les pages Studio dans StudioShellComponent (thème indigo D2)', () => {
    expect(STUDIO_ROUTES.length).toBe(1);
    expect(shell.path).toBe('');
    expect(shell.component).toBe(StudioShellComponent);
    expect(shell.children).toBe(STUDIO_CHILD_ROUTES);
    expect(STUDIO_CHILD_ROUTES.length).toBeGreaterThan(10);
  });

  it('protège chaque page par permissionGuard avec une liste de permissions', () => {
    for (const r of STUDIO_CHILD_ROUTES) {
      expect(r.canActivate?.length).withContext(r.path ?? '').toBe(1);
      expect(Array.isArray(r.data?.['permissions'])).withContext(r.path ?? '').toBeTrue();
    }
  });

  it('déclare ai, ai/projects et ai/templates avant systems/:key (sinon :key capturerait le segment)', () => {
    const paths = STUDIO_CHILD_ROUTES.map(r => r.path);
    const idx = (p: string) => paths.indexOf(p);
    expect(idx('ai')).toBeGreaterThan(-1);
    expect(idx('ai/projects')).toBeGreaterThan(idx('ai'));
    expect(idx('ai/templates')).toBeGreaterThan(idx('ai'));
    expect(idx('ai/projects')).toBeLessThan(idx('systems/:key'));
    expect(idx('ai/templates')).toBeLessThan(idx('systems/:key'));
    expect(idx('ai/templates')).toBeLessThan(idx(':id'));
  });

  it('donne un titre français aux pages du rail', () => {
    expect(child('ai/projects').title).toBe('Mes projets - InstaFact');
    expect(child('ai/templates').title).toBe('Bibliothèque de modèles - InstaFact');
    expect(typeof child('ai/projects').loadComponent).toBe('function');
    expect(typeof child('ai/templates').loadComponent).toBe('function');
  });
});

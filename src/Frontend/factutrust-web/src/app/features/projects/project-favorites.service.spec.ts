import { TestBed } from '@angular/core/testing';
import { ProjectFavoritesService } from './project-favorites.service';

describe('ProjectFavoritesService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  afterEach(() => localStorage.clear());

  it('starts empty when storage is missing', () => {
    const svc = TestBed.inject(ProjectFavoritesService);
    expect(svc.isFavorite('a')).toBe(false);
    expect(svc.favoriteIds()).toEqual([]);
  });

  it('toggles and persists favorites', () => {
    const svc = TestBed.inject(ProjectFavoritesService);
    expect(svc.toggle('p1')).toBe(true);
    expect(svc.isFavorite('p1')).toBe(true);
    expect(JSON.parse(localStorage.getItem('proj:favorites')!)).toEqual(['p1']);

    expect(svc.toggle('p1')).toBe(false);
    expect(svc.isFavorite('p1')).toBe(false);
    expect(JSON.parse(localStorage.getItem('proj:favorites')!)).toEqual([]);
  });

  it('ignores invalid JSON in storage', () => {
    localStorage.setItem('proj:favorites', '{bad');
    const svc = TestBed.inject(ProjectFavoritesService);
    expect(svc.favoriteIds()).toEqual([]);
  });
});

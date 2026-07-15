import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService, User } from '@core/services/auth.service';
import { FtPermissionDirective } from './ft-permission.directive';
import { PERMISSIONS } from '@core/config/permission-keys';

@Component({
  standalone: true,
  imports: [FtPermissionDirective],
  template: `
    <ng-container *ftPermission="PERMISSIONS.products.create">allowed</ng-container>
    <ng-container *ftPermission="keys; mode: 'any'">any</ng-container>
  `
})
class HostComponent {
  readonly PERMISSIONS = PERMISSIONS;
  keys = [PERMISSIONS.products.create, PERMISSIONS.products.delete] as const;
}

describe('FtPermissionDirective', () => {
  const baseUser: User = {
    id: 'u1',
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    fullName: 'A B',
    role: 'Accountant',
    roleDisplay: 'Compta',
    tenantId: '00000000-0000-0000-0000-000000000001',
    companyName: 'Co',
    twoFactorEnabled: false,
    effectivePermissions: ['products:create']
  };

  function setUser(auth: AuthService, user: User | null): void {
    (auth as unknown as { userSignal: { set: (u: User | null) => void } }).userSignal.set(user);
  }

  it('shows content when permission matches', () => {
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    const auth = TestBed.inject(AuthService);
    setUser(auth, baseUser);
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('allowed');
  });

  it('hides content when permission missing', () => {
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    const auth = TestBed.inject(AuthService);
    setUser(auth, { ...baseUser, effectivePermissions: ['products:read'] });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('allowed');
  });

  it('respects any mode', () => {
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    const auth = TestBed.inject(AuthService);
    setUser(auth, { ...baseUser, effectivePermissions: ['products:create'] });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('any');
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';
import { AuthService } from '@core/services/auth.service';
import { of } from 'rxjs';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('markAllAsTouched when submitting invalid form', () => {
    const markSpy = spyOn(component.form, 'markAllAsTouched').and.callThrough();
    component.onSubmit();
    expect(markSpy).toHaveBeenCalled();
  });

  it('calls authService.login on valid submit', () => {
    const authService = TestBed.inject(AuthService);
    spyOn(authService, 'login').and.returnValue(
      of({ success: true, data: null as never, message: null, errors: [] })
    );

    component.form.setValue({
      email: 'test@example.com',
      password: 'SecurePass123!',
      rememberMe: false,
    });

    component.onSubmit();
    expect(authService.login).toHaveBeenCalled();
  });
});

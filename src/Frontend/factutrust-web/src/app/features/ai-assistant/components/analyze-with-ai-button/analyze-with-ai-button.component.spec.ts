import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AnalyzeWithAiButtonComponent } from './analyze-with-ai-button.component';
import { AuthService } from '@core/services/auth.service';
import { AiScreenAnalysisService } from '../../services/ai-screen-analysis.service';

@Component({
  standalone: true,
  imports: [AnalyzeWithAiButtonComponent],
  template: `
    <app-analyze-with-ai-button
      screenId="test-screen"
      density="toolbar"
      [payloadBuilder]="payload" />
  `
})
class HostComponent {
  payload = () => ({ ok: true });
}

describe('AnalyzeWithAiButtonComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        {
          provide: AuthService,
          useValue: {
            hasAllPermissions: () => true,
            isAccountingFirm: () => false,
            isDelegatedMode: () => false
          }
        },
        {
          provide: AiScreenAnalysisService,
          useValue: { startAnalysis: jasmine.createSpy('startAnalysis') }
        }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('applies toolbar density host class', () => {
    const host = fixture.nativeElement.querySelector('app-analyze-with-ai-button');
    expect(host?.classList.contains('analyze-density-toolbar')).toBe(true);
  });
});

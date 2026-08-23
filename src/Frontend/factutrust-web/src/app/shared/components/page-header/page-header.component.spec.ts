import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PageHeaderComponent } from './page-header.component';

describe('PageHeaderComponent', () => {
  let fixture: ComponentFixture<PageHeaderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PageHeaderComponent]
    }).compileComponents();
    fixture = TestBed.createComponent(PageHeaderComponent);
  });

  it('does not render hint icon when hint is absent', () => {
    fixture.componentRef.setInput('title', 'Projets');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.page-title-hint')).toBeNull();
  });

  it('renders hint icon when hint is provided', () => {
    fixture.componentRef.setInput('title', 'Projets');
    fixture.componentRef.setInput('hint', 'Info test');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.page-title-hint')).not.toBeNull();
  });
});

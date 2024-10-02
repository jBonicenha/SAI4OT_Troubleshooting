import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DiagramGeneratorComponent } from './diagram-generator.component';

describe('DiagramGeneratorResultComponent', () => {
  let component: DiagramGeneratorComponent;
  let fixture: ComponentFixture<DiagramGeneratorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DiagramGeneratorComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(DiagramGeneratorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

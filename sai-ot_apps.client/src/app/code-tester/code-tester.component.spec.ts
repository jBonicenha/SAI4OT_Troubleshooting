import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { CodeTesterComponent } from './code-tester.component';
import { of } from 'rxjs/internal/observable/of';

describe('CodeTesterComponent', () => {
  let component: CodeTesterComponent;
  let fixture: ComponentFixture<CodeTesterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CodeTesterComponent],
      imports: [HttpClientTestingModule, ReactiveFormsModule] // Importações necessárias para os testes
    })
      .compileComponents();

    fixture = TestBed.createComponent(CodeTesterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should disable generate button if no file is selected', () => {
    const button = fixture.nativeElement.querySelector('button');
    expect(button.disabled).toBeTruthy();
  });

  it('should enable generate button when a file is selected', () => {
    component.onFileSelected({ target: { files: [new File([''], 'test.xlsx')] } });
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button');
    expect(button.disabled).toBeFalsy();
  });

  it('should show loading spinner when loading is true', () => {
    component.loading = true;
    fixture.detectChanges();
    const spinner = fixture.nativeElement.querySelector('#spinner');
    expect(spinner).toBeTruthy();
  });

  it('should not show loading spinner when loading is false', () => {
    component.loading = false;
    fixture.detectChanges();
    const spinner = fixture.nativeElement.querySelector('#spinner');
    expect(spinner).toBeFalsy();
  });

  it('should set SAICodeTesterResult when API call is successful', () => {
    const mockResult = { /* mock data */ };
    spyOn(component['http'], 'post').and.returnValue(of(mockResult)); // Substitua 'of' pela importação correta
    component.generateCodeTester();
    expect(component.SAICodeTesterResult).toEqual(mockResult);
  });

});

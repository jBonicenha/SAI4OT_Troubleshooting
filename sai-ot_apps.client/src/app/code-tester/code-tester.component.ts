import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-code-tester',
  templateUrl: './code-tester.component.html',
  styleUrls: ['./code-tester.component.scss']
})
export class CodeTesterComponent {
  profileForm: FormGroup;
  loading = false;
  file: File | null = null;
  SAICodeTesterResult: any;

  constructor(private http: HttpClient, private fb: FormBuilder) {
    this.profileForm = this.fb.group({
      folderPath: ['', Validators.required] // Adicione mais controles conforme necessário
    });
  }

  onFileSelected(event: any) {
    const fileInput = event.target as HTMLInputElement;
    if (fileInput.files && fileInput.files.length > 0) {
      this.file = fileInput.files[0];
    }
  }

  generateCodeTester() {
    if (this.file) {
      this.SAICodeTesterResult = '';
      this.loading = true; // Inicia o carregamento
      const url = `https://localhost:7070/api/CodeTester/validate-and-generate`;
      const formData = new FormData();
      formData.append('planilha', this.file, this.file.name);
      formData.append('folderPath', this.profileForm.get('folderPath')?.value);

      this.http.post<string>(url, formData, { responseType: 'json' }).subscribe(
        (data) => {
          this.SAICodeTesterResult = data || '';
          this.loading = false;
          this.profileForm.reset(); // Limpa o formulário após o sucesso, se necessário
          this.file = null; // Reseta a variável de arquivo
          const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
          if (fileInput) {
            fileInput.value = ''; // Reseta o input de arquivo
          }
        },
        (error) => {
          console.error('Error occurred:', error);
          alert('Failed to validate and generate CodeTester');
          this.loading = false;
        }
      );
    } else {
      alert('Please select a file before submitting.'); // Validação adicional
    }
  }
}

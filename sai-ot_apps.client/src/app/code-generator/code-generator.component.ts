import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormGroup, FormControl, FormBuilder } from '@angular/forms';
import { lastValueFrom } from 'rxjs';

@Component({
  selector: 'app-code-generator',
  templateUrl: './code-generator.component.html',
  styleUrls: ['./code-generator.component.scss']
})
export class CodeGeneratorComponent implements OnInit {
  constructor(private http: HttpClient, private router: Router, private fb: FormBuilder) { }

  loading: boolean = false;
  SAIRoutineCode: string = '';
  //selectedOption: string = 'create'; // Opção padrão
  SAICodeGeneratorXML: string | null = null;
  // Definindo o array de opções para o menu dropdown
  codeGeneratorMenu: string[] = ['Generate PLC routine code using natural language', 'Generate PLC code from a Interlock Table'];

  // Propriedade para armazenar a opção selecionada
  selectedOption: string = ''; // Opção inicial (pode ser 'Create Code')

  profileForm: FormGroup = new FormGroup({
    routineName: new FormControl(null),
    userRequest: new FormControl(null),
  });

  resultForm: FormGroup = new FormGroup({});

  file: File | null = null;

  ngOnInit() {
    // Inicializando o formulário
    this.profileForm = this.fb.group({
      routineName: [''],
      userRequest: [''],
      //codeGeneratorOption: [this.selectedOption]
      codeGeneratorOption: ['Generate PLC routine code using natural language']
    });

    this.resultForm = this.fb.group({});
  }

  // Função para alternar entre as opções do menu
  selectOption(option: string): void {
    this.selectedOption = option;
    this.resetForms();
  }

  // Função chamada ao mudar a opção no dropdown
  onOptionChange(event: any): void {
    this.selectedOption = event.target.value;
    // Aqui você pode fazer o que for necessário quando a opção for alterada, por exemplo:
    /*if (this.selectedOption === 'create') {
      // Ação para 'Create Code'
    } else if (this.selectedOption === 'use') {
      // Ação para 'Use a Code'
    }*/
  }

  // Função de geração de rotina PLC para a opção "Create Code"
  async generatePLCRoutine() {
    this.loading = true;
    const routineName = this.profileForm.get('routineName')?.value;
    const userRequest = this.profileForm.get('userRequest')?.value;

    await this.generatePLCRoutineCode(routineName, userRequest);
    this.loading = false;
  }

  // Função para fazer upload e gerar código usando arquivo na opção "Use a Code"
  async generateCodeGeneratorXML() {
    if (this.file) {
      this.SAICodeGeneratorXML = '';
      this.loading = true; // Inicia o carregamento
      const url = `https://localhost:7070/upload-generator-excel`;
      const formData = new FormData();
      formData.append('file', this.file, this.file.name);
      formData.append('folderPath', this.profileForm.get('folderPath')?.value);

      // Define responseType como 'text' para receber uma string no response
      this.http.post(url, formData, { responseType: 'text' }).subscribe(
        (data: string) => {
          // Armazena o conteúdo da resposta em SAICodeAuditorInterlock
          this.SAICodeGeneratorXML = data;
          console.log(data);

          // Limpa o estado e formulário após o sucesso
          this.loading = false;
          this.profileForm.reset(); // Limpa o formulário após o sucesso
          this.file = null; // Reseta a variável de arquivo
          const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
          if (fileInput) {
            fileInput.value = ''; // Reseta o input de arquivo
          }
        },
        (error) => {
          console.error('Error occurred:', error);
          alert('Failed to validate and generate CodeAuditorInterlock');
          this.loading = false;
        }
      );
    } else {
      alert('Please select a file before submitting.'); // Validação adicional
    }
  }

  // Função de download de código PLC gerado
  async downloadPLCRoutineCode() {
    const routineName = this.profileForm.get('routineName')?.value;
    await this.downloadPLCCode(routineName);
  }

  // Função para chamada de geração de rotina PLC
  async generatePLCRoutineCode(routineName: string, userRequest: string): Promise<void> {
    const url = `https://localhost:7070/generatePLCRoutineCode?routineName=${encodeURIComponent(routineName)}&userRequest=${encodeURIComponent(userRequest)}`;
    try {
      const data: string = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
      this.SAIRoutineCode = data || '';
    } catch (error) {
      console.error(error);
      alert('Failed to fetch generatePLCRoutineCode');
    }
  }

  // Função para download da rotina PLC
  async downloadPLCCode(routineName: string): Promise<void> {
    const url = `https://localhost:7070/downloadPLCRoutineCode?routineName=${encodeURIComponent(routineName)}`;
    try {
      const data: string = await lastValueFrom(this.http.post<string>(url, JSON.stringify(this.SAIRoutineCode), { headers: { 'Content-Type': 'application/json' }, responseType: 'text' as 'json' }));
      console.log('Routine downloaded successfully:', data);
    } catch (error) {
      console.error(error);
      alert('Failed to update routine with comments');
    }
  }

  // Manipulador para selecionar arquivo
  /*onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.file = input.files[0];
    }
  }*/

  onFileSelected(event: any): void {
    this.file = event.target.files[0];
  }

  // Função para resetar formulários e estado do arquivo
  resetForms(): void {
    this.profileForm.reset();
    this.file = null;
    this.SAIRoutineCode = '';
  }

  downloadXMLFile() {
    // Verifica se o conteúdo XML existe
    if (!this.SAICodeGeneratorXML) return;

    // Cria um blob com o conteúdo XML
    const blob = new Blob([this.SAICodeGeneratorXML], { type: 'application/xml' });

    // Cria uma URL para o blob e aciona o download
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'SAICodeGenerator.L5X'; // Nome do arquivo baixado
    a.click();
    window.URL.revokeObjectURL(url); // Limpa a URL para liberar memória
  }

}

import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormGroup, Validators, FormControl, FormsModule, ReactiveFormsModule, FormControlName, FormBuilder } from '@angular/forms';
import { firstValueFrom, lastValueFrom } from 'rxjs';
export interface Rung {
  Rung: string;
  Comment?: string;
  Logic?: string;
  Mistake?: string;
  Suggestion?: string;
}
interface RungDescription {
  rungNumber: string;
  description: string;
}

@Component({
  selector: 'app-code-auditor',
  templateUrl: './code-auditor.component.html',
  styleUrl: './code-auditor.component.scss'
})

export class CodeAuditorComponent implements OnInit {

  loading: boolean = false;
  PLCFilePath: string = '';
  routineName: string = '';

  profileForm: FormGroup = new FormGroup({
    folderPath: new FormControl(null),
    selectedOption: new FormControl('Text'),
    codeAuditorOption: new FormControl('Text')
  });

  outputFormats: string[] = [];
  codeAuditorMenu: string[] = ['AI Description creator editor', 'Interlock Map Generator', 'Equipment Auditor', 'Logic Auditor'];
  result: string = '';

  codeGenForm: FormGroup = new FormGroup({
    userStories: new FormControl(null, [Validators.required]),
    databaseType: new FormControl("sqlserver", []),
    newDescription: new FormControl('')
  });


  SAIPreRungAnalysis: string = '';
  SAIUDTAnalysis: string = '';
  SAIRungAnalysis: Rung[] = [];
  RoutineDescriptionRevised: RungDescription[] = [];
  //SAIRungAnalysis: { [key: string]: string[] } = {};
  routineCode: string = '';
  currentRung: Rung = { Rung: '', Comment: '', Logic: '', Mistake: '', Suggestion: '' };
  currentIndex: number = 0;
  progress: number = 0;
  Option1Selected: boolean = false;
  OptionSelected: number = 0;
  UserSkipComment: boolean = true;
  logicImagePath: string = '';
  temporaryImages: string[] = [];
  currentImageIndex: number = 0;
  currentImagePath: string | undefined;  // Caminho da imagem atual
  analysisCompleted: boolean = false; // Controle do alerta de sucesso
  // Declara a variável showAlert
  showAlert = false;
  alertMessage = 'Rung Analysis Completed!';

  constructor(
    private http: HttpClient,
    private router: Router,
    private fb: FormBuilder,
    private cdr: ChangeDetectorRef
  ) {
    this.profileForm = this.fb.group({
      folderPath: new FormControl(null),
      selectedOption: new FormControl('Text'),
      codeAuditorOption: new FormControl('Text')
    });
  }

  get selectedOptions() {
    return this.profileForm.get('selectedOption')?.value;
  }

  //Main Request - UDT Analysis
  async auditUDTMain(): Promise<void> {

    this.loading = true;//
    await this.UDTAnalysis(this.PLCFilePath);

  }

  /*//Main Request - Description Analysis
  async auditDescriptionMain(): Promise<void> {
    try
    {
      this.progress = 0;
      this.loading = true;
      this.SAIPreRungAnalysis = '';
      this.RoutineDescriptionRevised = [];
      // Get the values from FORM
      this.routineName = this.profileForm.get('selectedOption')?.value;

      // Ensure PLCFilePath is not empty or null
      if (!this.PLCFilePath) {
        throw new Error('PLC file path is required.');
      }

      // Await the result of GetRoutineByName
      await this.GetRoutineByName(this.PLCFilePath, this.routineName);

      // Call SAIDescriptionAnalysis only after GetRoutineByName completes
      await this.SAIDescriptionAnalysis(this.routineCode);

      this.logicImagePath = `../assets/img/${this.routineName}/${this.currentRung.Rung.replace(':', '.png')}`;
      console.log('logicImagePath:', this.logicImagePath);
    }
    catch (error)
    {
      console.error('Error in auditDescriptionMain:', error);
      alert('An error occurred during the audit process.');
    }
  }*/

  //Main Request - Description Analysis
  /*async auditDescriptionMain(): Promise<void> {
    try {
      this.progress = 0;
      this.loading = true;
      this.SAIPreRungAnalysis = '';
      this.RoutineDescriptionRevised = [];

      // Obter os valores do FORM
      this.routineName = this.profileForm.get('selectedOption')?.value;

      // Garantir que PLCFilePath não esteja vazio ou nulo
      if (!this.PLCFilePath) {
        throw new Error('PLC file path is required.');
      }

      // Await the result of GetRoutineByName
      await this.GetRoutineByName(this.PLCFilePath, this.routineName);

      // Processar a análise da descrição após a extração e carregamento de imagens
      //await this.SAIDescriptionAnalysis(this.routineCode);

      // Chamar a função para extrair rungs do PDF e obter o caminho da pasta temporária
      const outputDirectory = await this.extractRungsFromPdf();

      // Carregar a primeira imagem da pasta temporária
      if (this.temporaryImages && this.temporaryImages.length > 0) {
        //this.logicImagePath = this.temporaryImages[this.currentImageIndex];
        this.logicImagePath = `${outputDirectory}/${this.temporaryImages[this.currentImageIndex]}`;
      } else {
        throw new Error('No images found in temporary directory.');
      }

      // Processar a análise da descrição após a extração e carregamento de imagens
      await this.SAIDescriptionAnalysis(this.routineCode);

      console.log('logicImagePath:', this.logicImagePath);
    } catch (error) {
      console.error('Error in auditDescriptionMain:', error);
      alert('An error occurred during the audit process.');
    } finally {
      this.loading = false;
    }
  }*/

  async auditDescriptionMain(): Promise<void> {
    try {
      this.progress = 0;
      this.loading = true;
      this.SAIPreRungAnalysis = '';
      this.RoutineDescriptionRevised = [];

      // Obter o valor da rotina do formulário
      this.routineName = this.profileForm.get('selectedOption')?.value;

      // Verificar se PLCFilePath está preenchido
      if (!this.PLCFilePath) {
        throw new Error('O caminho do arquivo PLC é obrigatório.');
      }

      // Obter a rotina com o nome especificado
      await this.GetRoutineByName(this.PLCFilePath, this.routineName);

      // Extrair rungs do PDF e obter o diretório de saída
      const imageUrls = await this.extractRungsFromPdf();

      /*// Verificar se as imagens foram carregadas e definir o caminho da primeira imagem
      if (this.temporaryImages && this.temporaryImages.length > 0) {
        // Atribui a primeira imagem do diretório temporário a `logicImagePath`
        //this.logicImagePath = `${outputDirectory}/${this.temporaryImages[this.currentImageIndex]}`;
        await this.loadTemporaryImages(imageUrls); // Carrega as URLs das imagens
        this.logicImagePath = `${this.temporaryImages[this.currentImageIndex]}`;
      } else {
        throw new Error('Nenhuma imagem encontrada no diretório temporário.');
      }*/
      // Verificar se as imagens foram carregadas e definir o caminho da primeira imagem
      if (imageUrls && imageUrls.length > 0) {
        // Atualiza `temporaryImages` com as URLs retornadas
        this.temporaryImages = imageUrls;
        this.currentImageIndex = 0;

        // Configura o caminho da primeira imagem para exibição
        //this.logicImagePath = this.temporaryImages[this.currentImageIndex];
        this.logicImagePath = `https://localhost:7070${this.temporaryImages[this.currentImageIndex]}`;
      } else {
        throw new Error('Nenhuma imagem encontrada no diretório temporário.');
      }

      // Chamar a função de análise da descrição com o código da rotina
      await this.SAIDescriptionAnalysis(this.routineCode);

      console.log('Caminho da lógica da imagem:', this.logicImagePath);
    } catch (error) {
      console.error('Erro em auditDescriptionMain:', error);
      alert('Ocorreu um erro durante o processo de auditoria.');
    } finally {
      this.loading = false;
    }
  }


  //Function resposible to user select the Option to Audit
  onOptionChange(event: Event): void {
    const selectedOption = (event.target as HTMLSelectElement).value;
    console.log('Selected option:', selectedOption);
    // Perform any additional actions based on the selected option
    switch (selectedOption) {
      case 'AI Description creator editor':
        //
        this.OptionSelected = 1;
        this.Option1Selected = true;
        this.PLCFilePath = this.profileForm.get('folderPath')?.value;
        this.routinesList();
        break;
      case 'Interlock Map Generator':
        //
        this.OptionSelected = 2;
        this.Option1Selected = false;
        break;
      case 'Equipment Auditor':
        //
        this.OptionSelected = 3;
        this.Option1Selected = false;
        break;
      case 'Logic Auditor':
        //
        this.OptionSelected = 4;
        break;
      default:
        this.OptionSelected = 0;
        console.log('Unknown option selected');
    }
  }

  postDatabaseSchema() {

  }

  /*//Function to Apply Comment
  applyComment() {
    const newDesc = this.codeGenForm.get('newDescription')?.value;
    const rungNumber = this.currentRung.Rung;
    this.RoutineDescriptionRevised.push({ rungNumber: rungNumber, description: newDesc });
    this.UserSkipComment = false;
    this.skipComment();  
  }*/

  // Function to Apply Comment and move to the next image
  applyComment() {
    const newDesc = this.codeGenForm.get('newDescription')?.value;
    const rungNumber = this.currentRung.Rung;

    // Adiciona a descrição revisada
    this.RoutineDescriptionRevised.push({ rungNumber: rungNumber, description: newDesc });

    this.UserSkipComment = false;

    // Passa para a próxima imagem
    //this.nextImage();
    this.skipComment();
  }


  /*//Go to the next rung 
  async skipComment() {
    //Stil interacting in the list
    if (this.currentIndex < this.SAIRungAnalysis.length - 1)
    {
      if (this.UserSkipComment)
      {
        const rungNumber = this.currentRung.Rung;
        this.RoutineDescriptionRevised.push({ rungNumber: rungNumber, description: 'SKIP' });
      }
      this.currentIndex++;
      this.progress = Math.round((this.currentIndex / this.SAIRungAnalysis.length) * 100);      
      this.setCurrentRung();

      //Change image path
      this.logicImagePath = `../assets/img/${this.routineName}/${this.currentRung.Rung.replace(':', '.png')}`;
      console.log('logicImagePath:', this.logicImagePath);

      // Manually trigger change detection
      this.cdr.detectChanges();
    }
    //End of the list
    else
    {
      if (this.currentIndex = this.SAIRungAnalysis.length) {
        this.progress = 100;
        this.currentRung = { Rung: '', Comment: '', Logic: '', Mistake: '', Suggestion: '' };
        this.codeGenForm.patchValue({newDescription: ''});
        alert('Rung Analysis Completed');

        this.UpdateRoutineWithComments(this.PLCFilePath, this.routineName, this.RoutineDescriptionRevised);
        this.RoutineDescriptionRevised = [];
      }
      this.currentIndex = 0;
      this.logicImagePath = '';
      this.cdr.detectChanges();
    }
    this.UserSkipComment = true;    
  }*/

  // Go to the next rung and image
  async skipComment() {
    // Se ainda houver elementos na lista
    if (this.currentIndex < this.SAIRungAnalysis.length - 1) {
      // Adiciona 'SKIP' ao comentário se o usuário optar por pular
      if (this.UserSkipComment) {
        const rungNumber = this.currentRung.Rung;
        this.RoutineDescriptionRevised.push({ rungNumber: rungNumber, description: 'SKIP' });
      }

      // Avança para o próximo item da lista
      this.currentIndex++;
      this.progress = Math.round((this.currentIndex / this.SAIRungAnalysis.length) * 100);
      this.setCurrentRung();

      // Atualiza o caminho da próxima imagem
      this.updateCurrentImagePath();

      //Atualiza Imagem
      this.nextImage();

      // Trigger manual de change detection
      this.cdr.detectChanges();
    }
    // Se chegou ao fim da lista
    else {
      if (this.currentIndex === this.SAIRungAnalysis.length - 1) {
        this.progress = 100;
        this.currentRung = { Rung: '', Comment: '', Logic: '', Mistake: '', Suggestion: '' };
        this.codeGenForm.patchValue({ newDescription: '' });
        if (this.progress == 100) {
          this.showAlert = true; // Exibe o alerta quando o progresso atinge 100
          //this.displayAlert('Rung Analysis Completed!');
        }
        //alert('Rung Analysis Completed');

        // Atualiza a rotina com os comentários
        this.UpdateRoutineWithComments(this.PLCFilePath, this.routineName, this.RoutineDescriptionRevised);
        this.RoutineDescriptionRevised = [];

        // Chama a função para deletar a pasta temporária
        this.deleteTemporaryImages();
      }
      // Reinicia o índice e o caminho da imagem
      this.currentIndex = 0;
      this.logicImagePath = '';
      this.cdr.detectChanges();
    }

    // Marca a flag de pular comentário como verdadeira
    this.UserSkipComment = true;
  }

  //During the revision processing set the current rung
  setCurrentRung() {
    if (this.SAIRungAnalysis.length > 0) {
      this.currentRung = this.SAIRungAnalysis[this.currentIndex];

      this.codeGenForm.patchValue({
        newDescription: this.currentRung.Suggestion
      });
    }
  }

  adjustHeight(event: Event): void {
    console.log('Input event triggered');
    const textarea = event.target as HTMLTextAreaElement;
    textarea.style.height = 'auto'; // Reset the height
    textarea.style.height = `${textarea.scrollHeight}px`; // Set the height to the scroll height
  }

  ngOnInit(): void {

  }

  //Based in PLC File Path extract the list of routines
  async routinesList() {
    const url = `https://localhost:7070/RoutinesList?PLCfilePath=${encodeURIComponent(this.PLCFilePath)}`;
    try {
      const data = await lastValueFrom(this.http.post<string[]>(url, null, { responseType: 'json' }));
      // Handle the response data as needed
      console.log('Response:', data);
      this.outputFormats = data || [];
    } catch (error) {
      console.error(error);
      alert('Failed to fetch routinesList');
    }
  }

  //Based in the PLC File Path and Routine Name extract the entire code for this routine
  async GetRoutineByName(PLCfilePath: string, routineName: string): Promise<void> {
    const url = `https://localhost:7070/GetRoutineByName?PLCfilePath=${encodeURIComponent(PLCfilePath)}&routineName=${encodeURIComponent(routineName)}`;
    try {
      const data: string = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
      // Handle the response data as needed
      this.routineCode = data;
    } catch (error) {
      console.error(error);
      alert('Failed to fetch GetRoutineByName');
    }
  }

  //SAI make the analysis based in the code provided
  async SAIDescriptionAnalysis(routineCode: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const url = `https://localhost:7070/SAIDescriptionAnalysis`;
      const body = { routineCode: routineCode }; // Send routineCode in the body
      this.http.post<{ preRungAnalysis: any, rungAnalysis: any }>(url, body, {}).subscribe(
        (data) => {
          this.SAIPreRungAnalysis = data.preRungAnalysis;
          this.SAIRungAnalysis = data.rungAnalysis;
          console.log('SAIRungAnalysis:', this.SAIRungAnalysis);
          console.log('data:', data);
          this.setCurrentRung();
          this.loading = false;
          resolve();
        },
        (error) => {
          console.error(error);
          alert('Failed to fetch SAIDescriptionAnalysis');
          reject(error);
        }
      );
    });
  }

  //UpdateRoutineWithComments
  async UpdateRoutineWithComments(PLCfilePath: string, routineName: string, RoutineDescriptionRevised: RungDescription[]): Promise<void> {
    const url = `https://localhost:7070/UpdateRoutineWithComments?PLCfilePath=${encodeURIComponent(PLCfilePath)}&routineName=${encodeURIComponent(routineName)}`;
    try {
      const data: string = await lastValueFrom(this.http.post<string>(url, RoutineDescriptionRevised, { headers: { 'Content-Type': 'application/json' }, responseType: 'text' as 'json' }));
      console.log('Routine updated successfully:', data);
    } catch (error) {
      console.error(error);
      alert('Failed to update routine with comments');
    }
  }

  //Extract all the PLCs UDTs and Analyze with SAI
  async UDTAnalysis(PLCfilePath: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const url = `https://localhost:7070/AuditUDTAnalysis?PLCfilePath=${encodeURIComponent(PLCfilePath)}`;
      this.http.post<string>(url, null, {}).subscribe(
        (data) => {
          this.SAIUDTAnalysis = data;
          this.loading = false;
          resolve();
        },
        (error) => {
          console.error(error);
          alert('Failed to fetch UDTAnalysis');
          reject(error);
        }
      );
    });
  }

  async extractRungsFromPdf1(): Promise<string> {
    this.loading = true;
    try {
      /*// Monta a URL da API com os parâmetros PLCFilePath e routineName
      const url = `https://localhost:7070/extract-rungs?plcFilePath=${encodeURIComponent(this.PLCFilePath)}&routineName=${encodeURIComponent(this.routineName)}`;

      // Faz a requisição ao backend para extrair rungs e obter o diretório de saída
      const outputDirectory: string = await lastValueFrom(this.http.post<string>(url, null));

      // Obtém as imagens da pasta temporária
      await this.loadTemporaryImages(outputDirectory);*/
      // Modifique o caminho do arquivo para terminar em .pdf
      const pdfFilePath = this.PLCFilePath.replace(/\.L5X$/i, ".pdf");
      console.log("Caminho PDF: ", pdfFilePath);

      const url = `https://localhost:7070/extract-rungs?plcFilePath=${encodeURIComponent(pdfFilePath)}&routineName=${encodeURIComponent(this.routineName)}`;
      //const url = `https://localhost:7070/extract-rungs?plcFilePath=C:/SAI/SAICodeAuditor/PLC_M45_1525_Dev.pdf&routineName=Alarm_Main`;

      /*// Parâmetros passados no corpo da requisição com o caminho atualizado
      const requestBody = {
        plcFilePath: pdfFilePath,
        routineName: this.routineName
      };*/

      // Log para verificar o URL
      console.log("URL da requisição: ", url);

      // Envia a requisição GET com os parâmetros no corpo
      //const outputDirectory: string = await lastValueFrom(this.http.get<string>(url));
      //console.log("output Directory: ", outputDirectory);
      /*this.http.get<{ outputDirectory: string }>(url).subscribe(
        response => {
          console.log('Output Directory:', response.outputDirectory);
          // Lógica para lidar com as imagens...
        },
        error => {
          console.error('Error extracting rungs from PDF:', error);
        }
      );*/

      // Envia a requisição GET com os parâmetros e aguarda a resposta com lastValueFrom
      //const response = await lastValueFrom(this.http.get<{ outputDirectory: string }>(url));
      //console.log('Output Directory:', response.outputDirectory);

      // Envia a requisição GET e aguarda a resposta
      const response = await lastValueFrom(this.http.get<{ outputDirectory: string, imageUrls: string[] }>(url));
      console.log('Output Directory:', response.outputDirectory);
      console.log('Image URLs:', response.imageUrls);

      // Carregar as imagens da pasta temporária
      if (response.outputDirectory && response.imageUrls.length > 0) {
        await this.loadTemporaryImages(response.imageUrls);
        return response.outputDirectory;
      } else {
        alert('Nenhuma imagem encontrada na pasta temporária.');
        return '';
      }
    } catch (error) {
      console.error('Error extracting rungs from PDF:', error);
      alert('Failed to extract rungs from PDF');
      return '';
    } finally {
      this.loading = false;
    }
  }

  /*async extractRungsFromPdf(): Promise<void> { //2a opção
    this.loading = true;
    try {
      // Modifique o caminho do arquivo para terminar em .pdf
      const pdfFilePath = this.PLCFilePath.replace(/\.L5X$/i, ".pdf");
      console.log("Caminho PDF: ", pdfFilePath);

      // Monta a URL da API com os parâmetros PLCFilePath e routineName
      const url = `https://localhost:7070/extract-rungs?plcFilePath=${encodeURIComponent(pdfFilePath)}&routineName=${encodeURIComponent(this.routineName)}`;

      // Log para verificar o URL
      console.log("URL da requisição: ", url);

      // Envia a requisição GET e aguarda a resposta com `outputDirectory` e `imageUrls`
      const response = await lastValueFrom(this.http.get<{ imageUrls: string[] }>(url));
      console.log('Image URLs:', response.imageUrls);

      // Carrega as imagens usando as URLs retornadas do backend
      if (response.imageUrls && response.imageUrls.length > 0) {
        this.temporaryImages = response.imageUrls;
        this.currentImageIndex = 0;
        this.updateCurrentImagePath(); // Atualiza o caminho da imagem atual
      } else {
        alert('Nenhuma imagem encontrada na pasta temporária.');
      }
    } catch (error) {
      console.error('Erro ao extrair rungs do PDF:', error);
      alert('Falha ao extrair rungs do PDF');
    } finally {
      this.loading = false;
    }
  }*/

  async extractRungsFromPdf(): Promise<string[]> {
    this.loading = true;
    try {
      // Modifique o caminho do arquivo para terminar em .pdf
      const pdfFilePath = this.PLCFilePath.replace(/\.L5X$/i, ".pdf");
      console.log("Caminho PDF: ", pdfFilePath);

      // Monta a URL da API com os parâmetros PLCFilePath e routineName
      const url = `https://localhost:7070/extract-rungs?plcFilePath=${encodeURIComponent(pdfFilePath)}&routineName=${encodeURIComponent(this.routineName)}`;

      // Log para verificar o URL
      console.log("URL da requisição: ", url);

      // Envia a requisição GET e aguarda a resposta com `outputDirectory` e `imageUrls`
      const response = await lastValueFrom(this.http.get<{ imageUrls: string[] }>(url));
      console.log('Image URLs:', response.imageUrls);

      // Verificar se as imagens foram retornadas
      if (response.imageUrls && response.imageUrls.length > 0) {
        return response.imageUrls; // Retorna o array de URLs
      } else {
        alert('Nenhuma imagem encontrada na pasta temporária.');
        return []; // Retorna um array vazio se não houver imagens
      }
    } catch (error) {
      console.error('Erro ao extrair rungs do PDF:', error);
      alert('Falha ao extrair rungs do PDF');
      return []; // Retorna um array vazio em caso de erro
    } finally {
      this.loading = false;
    }
  }



  /*private async loadTemporaryImages(directoryPath: string): Promise<void> {
    // Endpoint para listar as imagens da pasta temporária
    const listUrl = `https://localhost:7070/ListImages?directoryPath=${encodeURIComponent(directoryPath)}`;
    try {
      const images = await lastValueFrom(this.http.get<string[]>(listUrl));
      this.temporaryImages = images;
      this.currentImageIndex = 0;
      this.updateCurrentImagePath();
    } catch (error) {
      console.error('Failed to load temporary images:', error);
      alert('Failed to load images from temporary directory');
    }
  }*/

  // Carrega as imagens da pasta temporária
  /*private async loadTemporaryImages(directoryPath: string): Promise<void> {
    const listUrl = `https://localhost:7070/ListImages?directoryPath=${encodeURIComponent(directoryPath)}`;
    try {
      const images = await lastValueFrom(this.http.get<string[]>(listUrl));
      this.temporaryImages = images;
      this.currentImageIndex = 0;
      this.updateCurrentImagePath();
    } catch (error) {
      console.error('Failed to load temporary images:', error);
      alert('Failed to load images from temporary directory');
    }
  }*/

  // Carrega as imagens a partir dos URLs fornecidos
  private async loadTemporaryImages(imageUrls: string[]): Promise<void> {
    try {
      // Armazena a lista de URLs de imagens para navegação
      this.temporaryImages = imageUrls;
      this.currentImageIndex = 0;
      this.updateCurrentImagePath(); // Atualiza a imagem exibida
    } catch (error) {
      console.error('Failed to load temporary images:', error);
      alert('Failed to load images from temporary directory');
    }
  }

  /*private updateCurrentImagePath(): void {
    if (this.temporaryImages.length > 0 && this.currentImageIndex < this.temporaryImages.length) {
      this.logicImagePath = this.temporaryImages[this.currentImageIndex];
      this.cdr.detectChanges();
    } else {
      this.logicImagePath = '';
    }
  }*/

  // Atualiza o caminho da imagem atual para exibição
  private updateCurrentImagePath(): void {
    if (this.temporaryImages && this.temporaryImages.length > 0) {
      //this.currentImagePath = this.temporaryImages[this.currentImageIndex];
      this.logicImagePath = `https://localhost:7070${this.temporaryImages[this.currentImageIndex]}`;
    }
  }


  nextImage(): void {
    if (this.currentImageIndex < this.temporaryImages.length - 1) {
      this.currentImageIndex++;
      this.updateCurrentImagePath();
    } else {
      alert('No more images to display.');
    }
  }

  previousImage(): void {
    if (this.currentImageIndex > 0) {
      this.currentImageIndex--;
      this.updateCurrentImagePath();
    }
  }

  // Função para deletar o diretório temporário chamando o endpoint do backend
  deleteTemporaryImages() {
    this.http.delete('https://localhost:7070/delete-images', {
      responseType: 'text'
    }).subscribe({
      next: (response) => {
        console.log('Imagens excluídas com sucesso:', response);
      },
      error: (error) => {
        console.error('Erro ao excluir as imagens:', error);
      }
    });
  }

  // Método para fechar o alerta pop-up
  closeAlert() {
    this.showAlert = false;
  }

  // Exibe o pop-up com uma mensagem customizada
  displayAlert(message: string) {
    this.alertMessage = message;
    this.showAlert = true;
  }

}

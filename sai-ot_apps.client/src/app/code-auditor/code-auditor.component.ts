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
  currentImagePath: string | undefined;  // Actual image path
  analysisCompleted: boolean = false; 
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

  async auditDescriptionMain(): Promise<void> {
    try {
      this.progress = 0;
      this.loading = true;
      this.SAIPreRungAnalysis = '';
      this.RoutineDescriptionRevised = [];

      // Get the routine value from the form
      this.routineName = this.profileForm.get('selectedOption')?.value;

      // Check if PLCFilePath is provided
      if (!this.PLCFilePath) {
        throw new Error('PLC file path is required.');
      }

      // Get the routine with the specified name
      await this.GetRoutineByName(this.PLCFilePath, this.routineName);

      // Extract rungs from the PDF and get the output directory
      const imageUrls = await this.extractRungsFromPdf();

      // Check if the images were loaded and set the path for the first image
      if (imageUrls && imageUrls.length > 0) {
        // Update `temporaryImages` with the returned URLs
        this.temporaryImages = imageUrls;
        this.currentImageIndex = 0;

        // Set the path of the first image for display
        this.logicImagePath = `https://localhost:7070${this.temporaryImages[this.currentImageIndex]}`;
      } else {
        throw new Error('No images found in the temporary directory.');
      }

      // Call the description analysis function with the routine code
      await this.SAIDescriptionAnalysis(this.routineCode);

      console.log('Logic image path:', this.logicImagePath);
    } catch (error) {
      console.error('Error in auditDescriptionMain:', error);
      alert('An error occurred during the audit process.');
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

  // Function to Apply Comment and move to the next image
  applyComment() {
    const newDesc = this.codeGenForm.get('newDescription')?.value;
    const rungNumber = this.currentRung.Rung;

    // Add the revised description
    this.RoutineDescriptionRevised.push({ rungNumber: rungNumber, description: newDesc });

    this.UserSkipComment = false;

    // Move to the next image
    this.skipComment();
  }

  // Go to the next rung and image
  async skipComment() {
    // If there are still elements in the list
    if (this.currentIndex < this.SAIRungAnalysis.length - 1) {
      // Add 'SKIP' to the comment if the user chooses to skip
      if (this.UserSkipComment) {
        const rungNumber = this.currentRung.Rung;
        this.RoutineDescriptionRevised.push({ rungNumber: rungNumber, description: 'SKIP' });
      }

      // Move to the next item in the list
      this.currentIndex++;
      this.progress = Math.round((this.currentIndex / this.SAIRungAnalysis.length) * 100);
      this.setCurrentRung();

      // Update the path of the next image
      this.updateCurrentImagePath();

      // Update Image
      this.nextImage();

      // Manually trigger change detection
      this.cdr.detectChanges();
    }
    // If we have reached the end of the list
    else {
      if (this.currentIndex === this.SAIRungAnalysis.length - 1) {
        this.progress = 100;
        this.currentRung = { Rung: '', Comment: '', Logic: '', Mistake: '', Suggestion: '' };
        this.codeGenForm.patchValue({ newDescription: '' });
        if (this.progress == 100) {
          this.showAlert = true; // Display alert when progress reaches 100
          //this.displayAlert('Rung Analysis Completed!');
        }
        //alert('Rung Analysis Completed');

        // Update the routine with comments
        this.UpdateRoutineWithComments(this.PLCFilePath, this.routineName, this.RoutineDescriptionRevised);
        this.RoutineDescriptionRevised = [];

        // Call the function to delete the temporary folder
        this.deleteTemporaryImages();
      }
      // Reset the index and image path
      this.currentIndex = 0;
      this.logicImagePath = '';
      this.cdr.detectChanges();
    }

    // Mark the skip comment flag as true
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
          //console.log('SAIRungAnalysis:', this.SAIRungAnalysis);
          //console.log('data:', data);
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

  async extractRungsFromPdf(): Promise<string[]> {
    this.loading = true;
    try {
      // Change the file extension for .pdf (The same file must be in the same folder of .L5X)
      const pdfFilePath = this.PLCFilePath.replace(/\.L5X$/i, ".pdf");
      //console.log("Caminho PDF: ", pdfFilePath);

      // Makes the API URL with parameters PLCFilePath e routineName
      const url = `https://localhost:7070/extract-rungs?plcFilePath=${encodeURIComponent(pdfFilePath)}&routineName=${encodeURIComponent(this.routineName)}`;

      // Log para verificar o URL
      //console.log("URL da requisição: ", url);

      // Send the GET req and awaits
      const response = await lastValueFrom(this.http.get<{ imageUrls: string[] }>(url));
      //console.log('Image URLs:', response.imageUrls);

      // Check if the images are being returned
      if (response.imageUrls && response.imageUrls.length > 0) {
        return response.imageUrls; // Return the URLs array
      } else {
        alert('Nenhuma imagem encontrada na pasta temporária.');
        return []; // Returns an empty array in case of error
      }
    } catch (error) {
      //console.error('Erro ao extrair rungs do PDF:', error);
      alert('Falha ao extrair rungs do PDF');
      return []; // In case of Error, it displays an empty array
    } finally {
      this.loading = false;
    }
  }

  // Load the images based on the URLs
  private async loadTemporaryImages(imageUrls: string[]): Promise<void> {
    try {
      // Stores de URLs list navigation
      this.temporaryImages = imageUrls;
      this.currentImageIndex = 0;
      this.updateCurrentImagePath(); // Update the image displayed
    } catch (error) {
      alert('Failed to load images from temporary directory');
    }
  }

  // Update the current image path for the displayed image
  private updateCurrentImagePath(): void {
    if (this.temporaryImages && this.temporaryImages.length > 0) {
      this.logicImagePath = `https://localhost:7070${this.temporaryImages[this.currentImageIndex]}`;
    }
  }

  // Navigate to the next image
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

  // Delete temporary images after usage
  deleteTemporaryImages() {
    this.http.delete('https://localhost:7070/delete-images', {
      responseType: 'text'
    }).subscribe({
      next: (response) => {
        console.log('Temporary images deleted successfully: ', response);
      },
      error: (error) => {
        console.error('Error deleting temporary images:', error);
      }
    });
  }

  // Close the pop-up alert
  closeAlert() {
    this.showAlert = false;
  }

  // Display customized pop-up message
  displayAlert(message: string) {
    this.alertMessage = message;
    this.showAlert = true;
  }

}

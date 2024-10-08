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
    codeAuditorOption: new FormControl('Text'),
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
  SAIRungAnalysis: Rung[] = [];
  RoutineDescriptionRevised: RungDescription[] = [];
  //SAIRungAnalysis: { [key: string]: string[] } = {};
  routineCode: string = '';
  currentRung: Rung = { Rung: '', Comment: '', Logic: '', Mistake: '', Suggestion: ''};
  currentIndex: number = 0;
  progress: number = 0;
  Option1Selected: boolean = false;
  UserSkipComment: boolean = true;
  logicImagePath: string = '';
  constructor(private http: HttpClient, private router: Router, private fb: FormBuilder, private cdr: ChangeDetectorRef) { }

  get selectedOptions() {
    return this.profileForm.get('selectedOption')?.value;
  }

  //Main Request
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
  }

  //Function resposible to user select the Option to Audit
  onOptionChange(event: Event): void {
    const selectedOption = (event.target as HTMLSelectElement).value;
    console.log('Selected option:', selectedOption);
    // Perform any additional actions based on the selected option
    switch (selectedOption) {
      case 'AI Description creator editor':
        //
        this.Option1Selected = true;
        this.PLCFilePath = this.profileForm.get('folderPath')?.value;
        this.routinesList();
        break;
      case 'Option 2':
        //
        this.Option1Selected = false;
        break;
      case 'Option 3':
        //
        this.Option1Selected = false;
        break;
      default:
        console.log('Unknown option selected');
    }
  }

  postDatabaseSchema() {

  }

  //Function to Apply Comment
  applyComment() {
    const newDesc = this.codeGenForm.get('newDescription')?.value;
    const rungNumber = this.currentRung.Rung;
    this.RoutineDescriptionRevised.push({ rungNumber: rungNumber, description: newDesc });
    this.UserSkipComment = false;
    this.skipComment();  
  }

  //Go to the next rung 
  skipComment() {
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


}

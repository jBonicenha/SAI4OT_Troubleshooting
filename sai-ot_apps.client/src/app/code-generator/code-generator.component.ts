import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormGroup, FormControl } from '@angular/forms';
import { firstValueFrom, lastValueFrom } from 'rxjs';

@Component({
  selector: 'app-code-generator',
  templateUrl: './code-generator.component.html',
  styleUrl: './code-generator.component.scss'
})
export class CodeGeneratorComponent {
  constructor(private http: HttpClient, private router: Router) { }

  loading: boolean = false;
  SAIRoutineCode: string = '';

  profileForm: FormGroup = new FormGroup({
    routineName: new FormControl(null),
    userRequest: new FormControl(null),
  });

  resultForm: FormGroup = new FormGroup({

  });

  ngOnInit() {
    // Initialization logic here
  }

  async generatePLCRoutine() {
    //string routineName, string userResquest
    this.loading = true;
    var routineName = this.profileForm.get('routineName')?.value
    var userResquest = this.profileForm.get('userRequest')?.value

    await this.generatePLCRoutineCode(routineName, userResquest);

  }

  async downloadPLCRoutineCode() {
    var routineName = this.profileForm.get('routineName')?.value
    await this.downloadPLCCode(routineName);
  }

  //Call the SAI Apps that will generate a intepretation based on the data received
  async generatePLCRoutineCode(routineName: string, userRequest: string): Promise<void> {
    const url = `https://localhost:7070/generatePLCRoutineCode?routineName=${encodeURIComponent(routineName)}&userRequest=${encodeURIComponent(userRequest)}`;
    try {
      const data: string = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
      this.SAIRoutineCode = data || '';
      this.loading = false;
      //alert('File generate successfully!');
    } catch (error) {
      console.error(error);
      this.loading = false;
      alert('Failed to fetch generatePLCRoutineCode');
    }
  }

  //[FromQuery] string routineName, string PLCRoutineCode

  //UpdateRoutineWithComments
  async downloadPLCCode(routineName: string): Promise<void> {
    const url = `https://localhost:7070/downloadPLCRoutineCode?routineName=${encodeURIComponent(routineName)}`;
    try {
      //const data: string = await lastValueFrom(this.http.post<string>(url, this.SAIRoutineCode, { headers: { 'Content-Type': 'application/json' }, responseType: 'text' as 'json' }));
      const data: string = await lastValueFrom(this.http.post<string>(url, JSON.stringify(this.SAIRoutineCode), { headers: { 'Content-Type': 'application/json' }, responseType: 'text' as 'json' }));
      console.log('Routine downloaded successfully:', data);
    } catch (error) {
      console.error(error);
      alert('Failed to update routine with comments');
    }
  }

}



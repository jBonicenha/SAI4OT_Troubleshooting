import { Component, OnInit, NgModule } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormGroup, Validators, FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom, lastValueFrom } from 'rxjs';

export interface Template {
  Index: number;
  Name: string;
  Path: string;
  Converted: boolean;
}

@Component({
  selector: 'app-code-converter',
  templateUrl: './code-converter.component.html',
  styleUrl: './code-converter.component.scss'
})

export class CodeConverterComponent implements OnInit {

  loading: boolean = false;
  results: string[] = [];
  generateScreenResult: string = '';
  //templateList: string = '';
  localFolderPath: string = '';
  templateList: Template[] = [];
  

  profileForm: FormGroup = new FormGroup({
    folderPath: new FormControl(null)
  });

  constructor(private http: HttpClient, private router: Router) { }

  ngOnInit(): void {
  }


  generateTemplateList() {
    this.localFolderPath = this.profileForm.get('folderPath')?.value;
    this.ExtractTemplatesFromFile(this.localFolderPath);

  }


  async ExtractTemplatesFromFile(projectPath: string) {
    const url = `https://localhost:7070/ExtractTemplatesFromFile?projectPath=${encodeURIComponent(projectPath)}`;
    try {
      const data = await lastValueFrom(this.http.post<Template[]>(url, null, { responseType: 'text' as 'json' }));
      // Handle the response data as needed
      console.log('Response:', data);
      this.templateList = data;
    } catch (error) {
      console.error(error);
      alert('Failed to fetch postCodeConverterIgnitionTemplateList');
    }
  }




}

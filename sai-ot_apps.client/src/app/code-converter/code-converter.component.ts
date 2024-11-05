import { Component, OnInit, NgModule } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormGroup, Validators, FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { lastValueFrom } from 'rxjs';

export interface Template {
  index: number;
  name: string;
  templatePath: string;
  screenExists: boolean;
  screenConverted: boolean
}

@Component({
  selector: 'app-code-converter',
  templateUrl: './code-converter.component.html',
  styleUrls: ['./code-converter.component.scss']
})

export class CodeConverterComponent implements OnInit {

  loading: boolean = false;
  results: string[] = [];
  generateScreenResult: string = '';
  localFolderPath: string = '';
  templateList: Template[] = [];
  

  profileForm: FormGroup = new FormGroup({
    folderPath: new FormControl(null, Validators.required)
  });
  constructor(private http: HttpClient, private router: Router) { }

  ngOnInit(): void {
  }

  async generateTemplateList() {
    this.localFolderPath = this.profileForm.get('folderPath')?.value;
    this.templateList = [];
    this.loading = true;
    await this.extractTemplatesFromFile(this.localFolderPath);
    this.loading = false;
  }

  async extractTemplatesFromFile(projectPath: string): Promise<void> {
    const url = `https://localhost:7070/ExtractTemplatesFromFile?projectPath=${encodeURIComponent(projectPath)}`;
    try {
      const data = await lastValueFrom(this.http.post<Template[]>(url, null));
      // Handle the response data as needed
      this.templateList = data;
      console.log('Response:', this.templateList);
    } catch (error) {
      console.error(error);
      alert('Failed to fetch extractTemplatesFromFile');
    }
  }




}

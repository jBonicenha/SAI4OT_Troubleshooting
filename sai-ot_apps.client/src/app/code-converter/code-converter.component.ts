import { Component, OnInit, NgModule } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormGroup, Validators, FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom, lastValueFrom } from 'rxjs';

@Component({
  selector: 'app-code-converter',
  templateUrl: './code-converter.component.html',
  styleUrl: './code-converter.component.scss'
})
export class CodeConverterComponent implements OnInit {

  loading: boolean = false;
  results: string[] = [];
  generateScreenResult: string = '';
  templateList: string = '';
  localFolderPath: string = '';
  

  profileForm: FormGroup = new FormGroup({
    folderPath: new FormControl(null)
  });

  constructor(private http: HttpClient, private router: Router) { }

  ngOnInit(): void {
  }
  /*
  1) Insert the folder path
    a) Vision - Folder with only XML
    b) templatesJSON - Folder with JSON file that will be generated from SVG file
    c) Perspective - defaultScreen.zip and new screens
  2) The system generate the list of JSON component templates
  3) The system will generate the new screen

   1) Main
   2) XML Reader
   3) JSON Creator - Add all components in the JSON
   4) Write/Reader

  */

  async postSingleDocument() {
    this.localFolderPath = this.profileForm.get('folderPath')?.value;
    await this.postCodeConverterIgnitionTemplateList(this.localFolderPath);
    this.results = JSON.parse(this.templateList);
  }

  async generateScreen()
  {
    await this.CodeConverterIgnitionGenerateScreen(this.localFolderPath);
    this.generateScreenResult = 'This screen was generated in the Perspective folder! Now using your Ignition Designer import your new screen.';
  }

  async postCodeConverterIgnitionTemplateList(projectPath: string) {
    const url = `https://localhost:7070/CodeConverterIgnitionTemplateList?projectPath=${encodeURIComponent(projectPath)}`;
    try {
      const data = await lastValueFrom(this.http.post<string[]>(url, null, { responseType: 'text' as 'json' }));
      // Handle the response data as needed
      console.log('Response:', data);
      this.templateList = data.toString();
    } catch (error) {
      console.error(error);
      alert('Failed to fetch postCodeConverterIgnitionTemplateList');
    }
  }

  async CodeConverterIgnitionGenerateScreen(projectPath: string) {
    const url = `https://localhost:7070/CodeConverterIgnitionGenerateScreen?projectPath=${encodeURIComponent(projectPath)}`;
    try {
      await lastValueFrom(this.http.post<string[]>(url, null, { responseType: 'text' as 'json' }));
      // Handle the response data as needed
    } catch (error) {
      console.error(error);
      alert('Failed to fetch CodeConverterIgnitionGenerateScreen');
    }
  }


}

import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AppComponent } from './app.component';
import { MainPageComponent } from './main-page/main-page.component';
import { TroubleshootingComponent } from './troubleshooting/troubleshooting.component';
import { CodeConverterComponent } from './code-converter/code-converter.component';
import { DiagramGeneratorComponent } from './diagram-generator/diagram-generator.component';
import { CodeAuditorComponent } from './code-auditor/code-auditor.component';

const routes: Routes = [
  { path: '', component: MainPageComponent },
  { path: 'diagram-generator', component: DiagramGeneratorComponent },
  { path: 'troubleshooting', component: TroubleshootingComponent },
  { path: 'code-converter', component: CodeConverterComponent },
  { path: 'code-auditor', component: CodeAuditorComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})

export class AppRoutingModule { }

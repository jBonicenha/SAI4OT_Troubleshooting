import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { MainPageComponent } from './main-page/main-page.component';
import { TroubleshootingComponent } from './troubleshooting/troubleshooting.component';
import { LeftMenuComponent } from './left-menu/pages/left-menu.component';
import { LeftMenuRoutingModule } from './left-menu/left-menu.routing.module';
import { CodeConverterComponent } from './code-converter/code-converter.component';
import { MarkdownModule } from 'ngx-markdown';
import { DiagramGeneratorComponent } from './diagram-generator/DiagramGeneratorComponent';
import { CodeAuditorComponent } from './code-auditor/code-auditor.component';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { CodeTesterComponent } from './code-tester/code-tester.component';
import { CodeGeneratorComponent } from './code-generator/code-generator.component';


@NgModule({ declarations: [
        LeftMenuComponent,
        AppComponent,
        DiagramGeneratorComponent,
        MainPageComponent,
        TroubleshootingComponent,
        CodeConverterComponent,
        CodeAuditorComponent,
        CodeTesterComponent,
        CodeGeneratorComponent
    ],
    bootstrap: [AppComponent],
    imports: [BrowserModule,
      AppRoutingModule, FormsModule, LeftMenuRoutingModule,
              ReactiveFormsModule, MarkdownModule.forRoot()],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideAnimationsAsync()]
})
export class AppModule { }




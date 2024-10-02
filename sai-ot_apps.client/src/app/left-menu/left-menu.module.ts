import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LeftMenuComponent } from './pages/left-menu.component';
import { LeftMenuRoutingModule } from './left-menu.routing.module';
import { FormsModule, ReactiveFormsModule  } from '@angular/forms';

@NgModule({
  declarations: [
    LeftMenuComponent
  ],  
  imports: [
    CommonModule,
    LeftMenuRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    LeftMenuModule
  ],
  exports: [
    LeftMenuComponent
  ]
})
export class LeftMenuModule { }

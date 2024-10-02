import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { LeftMenuComponent } from './pages/left-menu.component';

const routes: Routes = [
  {
    path: '',
    component: LeftMenuComponent
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class LeftMenuRoutingModule { }

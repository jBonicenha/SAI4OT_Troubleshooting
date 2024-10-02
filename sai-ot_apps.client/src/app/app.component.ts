import { HttpClient } from '@angular/common/http';
import { Component, OnInit, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {
  title = 'sai-ot_apps.client';
  logoImg = "../../assets/img/logo1.png";
  userImg = "../../assets/img/profileImg.jpg";
  username = '';

}


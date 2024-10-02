import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';

@Component({
  selector: 'app-main-page',
  templateUrl: './main-page.component.html',
  styleUrls: ['./main-page.component.scss']
})
export class MainPageComponent implements OnInit {

  constructor(private http: HttpClient, private router: Router) { }

  navigateToOtherPage() {
    this.router.navigate(['/diagram-generator']);
  }

  navigateToTroubleshooting() {
    this.router.navigate(['/troubleshooting']);
  }

  ngOnInit() {
  }

}

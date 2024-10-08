import { AfterViewInit, NgModule, signal, Injectable } from '@angular/core';
import { HttpHeaders } from '@angular/common/http';
import { Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { catchError, delay, map } from 'rxjs/operators';
import { throwError, Observable } from 'rxjs';
 

interface NetworkDiagramResponse {
  eqpDetailsList: string[][];
  eqpConnectionsList: { [key: string]: string[] };
}
export interface Diagram {
  Index: number;
  Name: string;
  ProcessorType: string;
  MajorRev: string;
  ExportDate: string;
  IP: string;
  Connections: number;
  BackupProvided: boolean;
}

export function mapEqpDetailsToDiagrams(eqpDetailsLists: string[][]): Diagram[] {
  return eqpDetailsLists.map(eqpDetailsList => {
    const diagram: Diagram = {
      Index: 0,
      Name: '',
      ProcessorType: '',
      MajorRev: '',
      ExportDate: '',
      IP: '',
      Connections: 0,
      BackupProvided: false
    };

    eqpDetailsList.forEach(detail => {
      const [key, value] = detail.split('=');
      switch (key) {
        case 'Index':
          diagram.Index = Number(value);
          break;
        case 'Name':
          diagram.Name = value.replace(/"/g, '');
          break;
        case 'ProcessorType':
          diagram.ProcessorType = value.replace(/"/g, '');
          break;
        case 'MajorRev':
          diagram.MajorRev = value.replace(/"/g, '');
          break;
        case 'ExportDate':
          diagram.ExportDate = value.replace(/"/g, '');
          break;
        case 'IP':
          diagram.IP = value.replace(/"/g, ''); // Remove quotes if present
          break;
        case 'BackupProvided':
          diagram.BackupProvided = value.toLowerCase() === 'true';
          break;
        case 'Connections':
          diagram.Connections = Number(value);
          break;
      }
    });

    return diagram;
  });
}



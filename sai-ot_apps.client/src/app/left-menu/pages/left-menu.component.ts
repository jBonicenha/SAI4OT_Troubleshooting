import { Component } from '@angular/core';

@Component({
  selector: 'app-left-menu',
  templateUrl: './left-menu.component.html',
  styleUrl: './left-menu.component.scss'
})
export class LeftMenuComponent {
  leftMenu: any[] = [
    { name: 'Documentation Generator', link: "document-generator" },

    { id: "code-generator", name: 'Code Generator', parent: null },

    { name: 'Database Model', link: "code-generator/database", parent: "code-generator" },
    { name: 'Backend', link: "code-generator/backend", parent: "code-generator" },
    { name: 'Business Rules', link: "code-generator/business-rules", parent: "code-generator" },
    { name: 'Frontend', link: "code-generator/frontend", parent: "code-generator" },

    { name: 'Demo SAI Apps', link: "demo-sai-apps" },

    { id: "sai-ot", name: 'SAI OT Apps', parent: null },

    { name: 'Network Diagram', link: "diagram-generator", parent: "sai-ot" },
    { name: 'PLC Code Generator', link: "diagram-generator", parent: "sai-ot" },
    { name: 'PLC Code Auditor', link: "code-auditor", parent: "sai-ot" },
    { name: 'PLC Troubleshooting', link: "troubleshooting", parent: "sai-ot" },
    { name: 'Code Converter (Ignition)', link: "code-converter", parent: "sai-ot" },
    { name: 'Code Tester', link: "code-tester", parent: "sai-ot" }
  ];


  getMenu(parent: string = '') {
    if (!parent) {
      return this.leftMenu.filter(x => !x.parent);      
    }
    return this.leftMenu.filter(x => x.parent == parent);
  }

}

import { Component, OnInit, AfterViewInit, ViewChild, ElementRef, NgModule, signal } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormGroup, Validators, FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { GraphEditor, GraphEditorIn, GraphEditorOut, GraphInitConfig, GraphXmlData, ActionType, GraphEditorSVG, ButtonActionType } from '@zklogic/draw.io';

interface NetworkDiagramResponse {
  eqpDetailsList: string[][];
  eqpConnectionsList: { [key: string]: string[] };
}
interface Diagram {
  Index: number;
  Name: string;
  ProcessorType: string;
  MajorRev: string;
  ExportDate: string;
  IP: string;
  Connections: number;
  BackupProvided: boolean;
}

function mapEqpDetailsToDiagrams(eqpDetailsLists: string[][]): Diagram[] {
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


@Component({
  selector: 'app-diagram-generator',
  templateUrl: './diagram-generator.component.html',
  styleUrls: ['./diagram-generator.component.scss']
})
export class DiagramGeneratorComponent implements OnInit {
  @ViewChild('expandableContainer', { static: false }) expandableContainer!: ElementRef<HTMLElement> | any;
  @ViewChild('container', { static: false }) container!: ElementRef<HTMLElement> | any;
  @ViewChild('mxgraphScriptsContainer', { static: false }) mxgraphScriptsContainer!: ElementRef<HTMLElement> | any;
  @ViewChild('diagramContainer', { static: false }) diagramContainer: ElementRef | undefined;
  graphEditor: GraphEditor = new GraphEditor();
  diagrams: Diagram[] = [];
  eqpDetailsList: Diagram[] = [];
  eqpDetailsListJSON: string = "";
  eqpConnections: string = "";
  eqpConnectionsList: string = "";
  eqpConnectionsListKey: { [key: string]: string[] } = {};
  eqpConnectionsListAllRelated: { [key: string]: string[] } = {};
  diagramXMLresult: string = "";
  SAINetworkAnalysisResult: string = "";
  loading: boolean = false;
  loadingDiagram: boolean = false;
  selectedRow: number | null = null;


  profileForm: FormGroup = new FormGroup({
    folderPath: new FormControl(null)
  });

  constructor(private http: HttpClient, private router: Router) { }

  ngOnInit() {
    // Initialization logic here
  }

  //Function to open the draw.io in fulscreen
  openFullScreen() {
    // Ensure the element is available before trying to access it
    setTimeout(() => {
      if (!this.diagramContainer) {
        console.error('diagramContainer is not defined');
        return;
      }

      const elem = this.diagramContainer.nativeElement;
      if (elem) {
        if (elem.requestFullscreen) {
          elem.requestFullscreen();
        } else if (elem.mozRequestFullScreen) { /* Firefox */
          elem.mozRequestFullScreen();
        } else if (elem.webkitRequestFullscreen) { /* Chrome, Safari and Opera */
          elem.webkitRequestFullscreen();
        } else if (elem.msRequestFullscreen) { /* IE/Edge */
          elem.msRequestFullscreen();
        }
      } else {
        console.error('diagramContainer is not defined');
      }
    }, 0); // Delay to ensure the element is available
  }

  //Function to check if the IPs in the list belongs to the selected
  areIPsRelated(ip1: string, ip2: string): boolean {
    // Check if the two IPs are the same
    if (ip1 === ip2) return true;
    const formattedIp1 = `IP="${ip1}"`;

    // First level check: see if ip2 is directly connected to ip1
    if (this.eqpConnectionsListAllRelated[formattedIp1]?.includes(ip2)) {
      return true;
    }
    // Return false if we exhaust all options and don't find ip2
    return false;
  }

  //Enter in this function if any row in the table were clicked
  highlightRow(index: number) {
    this.selectedRow = index;    
  }

  //Run this function to the other rows
  isOtherRow(index: number): boolean {
    //Check which IPs are related with the selected one
    if (this.selectedRow !== null) {
      const selectedIP = this.eqpDetailsList[this.selectedRow].IP;
      const rowIP = this.eqpDetailsList[index].IP;
      const IPRelates = this.areIPsRelated(selectedIP, rowIP);
      return IPRelates && this.selectedRow !== index;
    }
    return false;

  }

  //Generate Button in the screen
  async generateNetworkDiagram() {
    try {

      this.loading = true;
      //Execute the Analysis at the PLC and extracted data related
      await this.plcAnalysis(this.profileForm.get('folderPath')?.value);
      //Based on the extracted data generate Analysis using SAI Prompt
      await this.SAINetworkAnalysis();

      //console.log('Eqp Connetion List:', this.eqpConnectionsList);
      //console.log('Eqp Details List:', this.eqpDetailsListJSON);

      //Generate the diagram using draw.io XML format
      await this.drawioXMLGenerator(this.eqpConnectionsList);
      //Temporary!
      //this.diagramXMLresult = '<mxGraphModel dx="1070" dy="647" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="827" pageHeight="1169" math="0" shadow="0"> <root> <mxCell id="0" /> <mxCell id="1" parent="0" /> <mxCell id="2" value="10.41.0.171" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="100" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="3" value="10.41.0.173" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="200" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="4" value="10.41.0.179" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="300" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="5" value="10.41.0.192" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="400" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="6" value="10.41.0.193" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="500" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="7" value="10.41.0.73" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="600" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="8" value="10.41.0.143" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="700" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="9" value="192.168.1.91" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="800" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="10" value="10.41.0.146" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="900" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="11" value="10.41.0.149" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1000" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="12" value="10.41.0.202" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1100" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="13" value="10.41.0.113" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1200" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="14" value="10.41.0.121" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1300" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="15" value="10.41.0.254" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1400" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="16" value="10.41.0.120" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1500" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="17" value="10.41.0.64" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1600" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="18" value="10.41.78.252" style="sketch=0;outlineConnect=0;fontColor=#232F3E;gradientColor=none;fillColor=#7AA116;strokeColor=none;dashed=0;verticalLabelPosition=bottom;verticalAlign=top;align=center;html=1;fontSize=12;fontStyle=0;aspect=fixed;pointerEvents=1;shape=mxgraph.aws4.iot_thing_plc;" vertex="1" parent="1"> <mxGeometry x="1700" y="100" width="50" height="50" as="geometry" /> </mxCell> <mxCell id="19" edge="1" parent="1" source="3" target="4"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="20" edge="1" parent="1" source="4" target="5"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="21" edge="1" parent="1" source="4" target="6"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="22" edge="1" parent="1" source="4" target="7"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="23" edge="1" parent="1" source="8" target="9"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="24" edge="1" parent="1" source="10" target="9"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="25" edge="1" parent="1" source="11" target="8"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="26" edge="1" parent="1" source="11" target="10"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="27" edge="1" parent="1" source="12" target="10"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="28" edge="1" parent="1" source="12" target="8"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="29" edge="1" parent="1" source="12" target="11"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="30" edge="1" parent="1" source="12" target="13"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="31" edge="1" parent="1" source="12" target="14"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="32" edge="1" parent="1" source="12" target="15"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="33" edge="1" parent="1" source="12" target="16"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="34" edge="1" parent="1" source="12" target="17"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="35" edge="1" parent="1" source="16" target="14"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="36" edge="1" parent="1" source="18" target="5"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="37" edge="1" parent="1" source="18" target="6"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="38" edge="1" parent="1" source="18" target="13"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="39" edge="1" parent="1" source="18" target="14"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="40" edge="1" parent="1" source="18" target="16"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="41" edge="1" parent="1" source="18" target="8"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="42" edge="1" parent="1" source="18" target="10"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="43" edge="1" parent="1" source="18" target="11"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="44" edge="1" parent="1" source="18" target="2"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="45" edge="1" parent="1" source="18" target="3"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="46" edge="1" parent="1" source="18" target="4"> <mxGeometry relative="1" as="geometry" /> </mxCell> <mxCell id="47" edge="1" parent="1" source="18" target="12"> <mxGeometry relative="1" as="geometry" /> </mxCell> </root> </mxGraphModel>';

      //Open Draw.io application and draw the diagram

      setTimeout(() => this.drawioBuilder(this.diagramXMLresult));

    }
    catch
    {
      this.loading = false;
    }

  }

  plcAnalysis(folderPath: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const url = `https://localhost:7070/api/NetworkDiagram/plcAnalysis?directoryPLC=${encodeURIComponent(folderPath)}`;
      this.http.post<{ eqpDetailsList: any, eqpConnectionsList: any, eqpConnectionsListAllRelated: any }>(url, null, {}).subscribe(
        (data) => {
          this.eqpConnectionsList = JSON.stringify(data.eqpConnectionsList); // Store the connections list if needed

          this.eqpConnectionsListKey = data.eqpConnectionsList;
          this.eqpConnectionsListAllRelated = data.eqpConnectionsListAllRelated;

          //console.log('eqpConnectionsList', this.eqpConnectionsList);
          //console.log('eqpConnectionsListAllRelated', this.eqpConnectionsListAllRelated);

          this.eqpDetailsList = mapEqpDetailsToDiagrams(data.eqpDetailsList);

          //console.log('eqpDetailsList', this.eqpDetailsList)
          this.eqpDetailsListJSON = JSON.stringify(this.eqpDetailsList);
          
          resolve();
        },
        (error) => {
          console.error(error);
          alert('Failed to fetch network diagrams');
          reject(error);
        }
      );
    });
  }

  //Call the SAI Apps that will generate a intepretation based on the data received
  SAINetworkAnalysis() {
    const url = `https://localhost:7070/api/NetworkDiagram/SAINetworkAnalysis?tableList=${encodeURIComponent(this.eqpDetailsListJSON)}&connectionList=${encodeURIComponent(this.eqpConnectionsList)}`;
    this.http.post<string>(url, null, { responseType: 'text' as 'json' }).subscribe(
      (data) => {
        this.SAINetworkAnalysisResult = data || '';
        this.loading = false;
        this.loadingDiagram = true;
      },
      (error) => {
        console.error(error);
        alert('Failed to fetch SAINetworkAnalysis');
      }
    );
  }

  //Using SAI Apps generate XML diagram based on the IP connection list
  drawioXMLGenerator(eqpConnectionList: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const url = `https://localhost:7070/api/NetworkDiagram/drawioXMLGenerator?EqpConnectionList=${encodeURIComponent(eqpConnectionList)}`;
      this.http.post<string>(url, null, {responseType: 'text' as 'json'}).subscribe(
        (data) => {
          this.diagramXMLresult = data.toString();
          console.log('diagramXMLresult', this.diagramXMLresult)
          resolve();
        },
        (error) => {
          console.error(error);
          alert('Failed to fetch drawioXMLGenerator');
          reject(error);
        }
      );
    });
  }

  parseDiagram(diagramArray: string[]): Diagram {
    const diagram: any = {};
    diagramArray.forEach(pair => {
      const [key, value] = pair.split('=');
      diagram[key.trim()] = value ? value.replace(/"/g, '').trim() : '';
    });
    diagram.BackupProvided = diagram.BackupProvided === 'True';
    return diagram as Diagram;
  }

  drawioBuilder(xml: string): void {
    //Div container to load Graph Editor
    this.graphEditor.initialized(this.container.nativeElement, this.mxgraphScriptsContainer.nativeElement, {
      actions: {
        subMenu: {
          save: (xml: GraphXmlData | GraphEditorSVG): Promise<GraphEditorOut> => {
            return new Promise((resolve, reject) => {
              //save data here
              resolve({
                status: "Data Saved",
                graphData: xml
              } as GraphEditorOut)
            });
          }
        }
      },
      actionsButtons: {
        'Export Library': {
          title: "Export To App Library",
          actionType: ActionType.EXPORTSVG,
          callback: this.graphEditorLibraryExportEvent,
          callbackOnError: this.graphEditorActionsErrorEvent,
          style: {
            backgroundColor: '#4d90fe',
            border: '1px solid #3079ed',
            backgroundImage: 'linear-gradient(#4d90fe 0,#4787ed 100%)',
            height: '29px',
            lineHeight: '25px'
          }
        } as ButtonActionType,
        'Import Library': {
          title: "Import From App Library",
          actionType: ActionType.OPEN,
          callback: this.graphEditorLibraryImportEvent,
          callbackOnFinish: this.graphEditorLibraryImportFinishEvent,
          style: {
            backgroundColor: '#4d90fe',
            border: '1px solid #3079ed',
            backgroundImage: 'linear-gradient(#4d90fe 0,#4787ed 100%)',
            height: '29px',
            lineHeight: '25px'
          }
        } as ButtonActionType
      },
      extraActions: {
        file: {
          exportAs: {
            'App Library': {
              actionType: ActionType.EXPORTSVG,
              callback: this.graphEditorLibraryExportEvent,
              callbackOnError: this.graphEditorActionsErrorEvent
            }
          },
          importFrom: {
            'App Library': {
              actionType: ActionType.OPEN,
              callback: this.graphEditorLibraryImportEvent,
              callbackOnFinish: this.graphEditorLibraryImportFinishEvent
            }
          }
        }
      }
    } as GraphInitConfig)
      .then(resolve => {
        console.log(resolve)
        //Fetch last saved graph data and set after initialization
        this.graphEditor.setGrapheditorData({ xml: xml } as GraphXmlData).then(resolve => {
          console.log("setGraphEditor", resolve)
        }, reject => {
          console.log("setGraphEditor", reject)
        }).catch(e => {
          console.log("setGraphEditor", e)
        });
      }, reject => {
        console.log(reject);
      })
    this.loadingDiagram = false;
  }

  graphEditorLibraryImportFinishEvent = (graphData: any): Promise<GraphEditorOut> => {
    return new Promise((resolve, reject) => {
      console.log('graphEditorLibraryImportFinishEvent', graphData);
      resolve({
        status: "Import App Library Implementation required",
        graphData: graphData
      })
    })
  }

  graphEditorLibraryImportEvent = (): Promise<GraphEditorIn> => {
    return new Promise((resolve, reject) => {
      resolve({
        status: (false ? "Okay" : "cancel"),
        graphData: (null) as any
      })
      // this.drawioImport.showDialog((data: any) => {
      //   console.log("callback:data", data);
      //   resolve({
      //     status: (data && data.drawio_data ? "Okay" : "cancel"),
      //     graphData: (data && data.drawio_data ? { xml: data.drawio_data.graphXmlData.xml, name: data.legal_name } : null) as any
      //   })
      // })
    })
  }

  graphEditorActionsErrorEvent = (graphData: any): Promise<GraphEditorOut> => {
    return new Promise((resolve, reject) => {
      console.log('graphEditorActionsErrorEvent', graphData);
      resolve({
        status: "Export App Library Implementation required",
        graphData: graphData
      })
    })
  }

  graphEditorLibraryExportEvent = (graphData: GraphEditorSVG): Promise<GraphEditorOut> => {
    return new Promise((resolve, reject) => {
      console.log("graphData", graphData);
      resolve({
        status: "TS Export App Library Implementation required",
        graphData: (graphData && graphData.xml ? { xml: graphData.xml, name: graphData.name } : null) as any
      })
    })
  }



}


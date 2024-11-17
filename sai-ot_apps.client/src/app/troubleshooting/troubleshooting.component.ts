import { Component, OnInit, NgModule, ElementRef } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom, lastValueFrom } from 'rxjs';

@Component({
  selector: 'app-troubleshooting',
  templateUrl: './troubleshooting.component.html',
  styleUrls: ['./troubleshooting.component.scss']
})
export class TroubleshootingComponent implements OnInit {
  messages: { user: string, text: string, showFormattedContent: boolean, formattedContent: string }[] = [];
  newMessage: string = '';
  diagramXMLresult: string = "";
  troubleshootingResultList: string = '';
  troubleshootingResultMessage: string = '';
  globalOTETag: string = '';
  OTETagLength: number = 0;
  currentUser = 'Alice';
  SAITroubleshootingCodeExplainerResult: string = "";
  SAITroubleshootingMenuResult: string = "";
  textMessage: string = "";

  constructor(private http: HttpClient, private router: Router) { }

  ngOnInit(): void {

  }

  sendMessage() {
    if (this.newMessage.trim()) {
      this.SAITroubleshootingCodeExplainerResult = "";
      this.messages.push({ user: 'User', text: this.newMessage, showFormattedContent: false, formattedContent:'' });
      this.textMessage = this.newMessage;
      //this.SAITroubleshootingChatRequest(this.newMessage);
      this.processingTroubleshooting();
      this.newMessage = '';
    }
  }

  async processingTroubleshooting() {

    //Read the message sent by user and try to extract the TAG
    const OTETagMessage: string = await this.SAITroubleshootingChatRequest(this.newMessage);
    //Based in the lenth try to determin if is TAG or NOT
    this.OTETagLength = OTETagMessage.length;
    if (this.OTETagLength > 20)
    {
      this.messages.push({ user: 'Server', text: OTETagMessage, showFormattedContent: false, formattedContent: '' });
    }
    else
    {
      //Otherwise, save in the globalOTETag variable
      this.globalOTETag = OTETagMessage || '';
      this.globalOTETag = this.globalOTETag; //this.globalOTETag.toUpperCase();
      this.messages.push({ user: 'Server', text: 'Based on your request, I will analyze the logic for tag: ' + this.globalOTETag, showFormattedContent: false, formattedContent: '' });

      //Based in the messsage sent by user check if will be needed "Troubleshooting" or "CodeExplainer"
      await this.SAITroubleshootingMenu();
      //If is TB "Troubleshooting"
      if (this.SAITroubleshootingMenuResult == "TB") {
        //If the current equipment is RUNNING
        let OTETagRunning: boolean = await this.OPCClient();
        if (OTETagRunning)
        {
          this.messages.push({ user: 'Server', text: 'The equipment is running!', showFormattedContent: false, formattedContent: '' });
        }
        //If the equipment is STOPPED execute TROUBLESHOOTING
        else
        {
          await this.getTroubleshootingResult(this.globalOTETag);
          await this.SAITroubleshootingChatResult();
        }

      }
      //If is CE "CodeExplainer"
      else
      {
        this.SAITroubleshootingCodeExplainer();      
      }
    }
  }

  //Call the API that read the user message and extract the OTETag
  async SAITroubleshootingChatRequest(chatRequest: string): Promise<string> {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingChatRequest?chatRequest=${encodeURIComponent(chatRequest)}`;
    try {
      const data = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
      console.log('Response:', data);
      return data || '';
    } catch (error) {
      console.error(error);
      alert('Failed to fetch SAITroubleshootingChatRequest.');
      return '';
    }
  }

  //Call the API that find the tags that affect the OTETag result
  async getTroubleshootingResult(OTETag: string) {
    const url = `https://localhost:7070/api/Troubleshooting/TroubleshootingProgram?OTETag=${encodeURIComponent(OTETag)}`;
    try {
      const data = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
      // Handle the response data as needed
      console.log('Response:', data);
      this.troubleshootingResultList = data || '';
    } catch (error) {
      console.error(error);
      alert('Failed to fetch getTroubleshootingResult.');
    }
  }

  SAITroubleshootingChatResult() {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingChatResult?ogTag=${encodeURIComponent(this.globalOTETag)}&allWrongTags=${encodeURIComponent(this.troubleshootingResultList)}`;
    this.http.post<string>(url, null, { responseType: 'text' as 'json' }).subscribe(
      async (data) => {
        // If the count is greater than 30, push to messages
        this.troubleshootingResultMessage = data || '';
        //this.messages.push({ user: 'Server', text: this.troubleshootingResultMessage, showFormattedContent: false, formattedContent: '' });
        this.messages.push({ user: 'Server', text: '', showFormattedContent: true, formattedContent: this.troubleshootingResultMessage });
        console.log('Response:', data);
      },
      (error) => {
        console.error(error);
        alert('Failed to fetch SAITroubleshootingChatResult.');
      }
    );
  }

  SAITroubleshootingCodeExplainer() {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingCodeExplainer?tagName=${encodeURIComponent(this.globalOTETag)}`;
    this.http.post<string>(url, null, { responseType: 'text' as 'json' }).subscribe(
      async (data) => {
        this.SAITroubleshootingCodeExplainerResult = data || '';
        this.messages.push({ user: 'Server', text: '', showFormattedContent: true, formattedContent: this.SAITroubleshootingCodeExplainerResult });
      },
      (error) => {
        console.error(error);
        alert('Failed to fetch SAITroubleshootingCodeExplainer.');
      }
    );
  }

  //SAITroubleshootingMenu
  async SAITroubleshootingMenu() {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingMenu?msgInput=${encodeURIComponent(this.textMessage)}`;
    try {
      const data = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
      // Handle the response data as needed
      console.log('Response:', data);
      this.SAITroubleshootingMenuResult = data || '';
    } catch (error) {
      console.error(error);
      alert('Failed to fetch SAITroubleshootingMenu.');
    }
  }

  
  //SAITroubleshootingMenu
  async OPCClient(): Promise<boolean> {
    const url = `https://localhost:7070/api/Troubleshooting/OPCClient?tagList=${encodeURIComponent(this.globalOTETag)}`;
    try {
      const data = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
      // Handle the response data as needed
      console.log('Response:', data);
      const parts = data.split('=');
      if (parts.length !== 2) {
        return false;
      }
      const booleanString = parts[1].trim().toLowerCase();
      return booleanString === 'true';

    } catch (error) {
      console.error(error);
      alert('Failed to fetch OPCClient.');
      return false;
    }
  }

}

/*Message sequence
1) Receive the message from the user
2) Extract the tag from text
  YES - Go to 3
  NO - If is not possible extract the tag from text, give the PROMPT answer
3) Run the Backend code passing the tag
3) Get the response
  YES - If is a list o tags go to 4
  NO - If is a error message, just print
4) Generate a response based on the list of tags

*/

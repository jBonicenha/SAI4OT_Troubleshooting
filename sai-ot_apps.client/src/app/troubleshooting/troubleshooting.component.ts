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
  troubleshootingConsolidatedResult: string = "";
  SAITroubleshootingMenuResult: string = "";
  textMessage: string = "";
  loading: boolean = false;

  constructor(private http: HttpClient, private router: Router) { }

  ngOnInit(): void {

  }

  sendMessage() {
    if (this.newMessage.trim()) {
      this.SAITroubleshootingCodeExplainerResult = "";
      this.messages.push({ user: 'User', text: this.newMessage, showFormattedContent: false, formattedContent:'' });
      this.textMessage = this.newMessage;
      this.loading = true;
      this.processingTroubleshooting();
      this.newMessage = '';
    }
  }

  async processingTroubleshooting() {

    //Based in the messsage sent by user check if will be needed "Troubleshooting" or "CodeExplainer" and extract the main TAG
    await this.SAITroubleshootingMenu();

    //If the response was not as expected, send other message
    if ((this.globalOTETag.length < 1 && this.SAITroubleshootingMenuResult.length < 1) || this.globalOTETag.length > 30)
    {
      this.messages.push({ user: 'Server', text: this.globalOTETag, showFormattedContent: false, formattedContent: '' });
      this.loading = false;
    }
    else
    {
      //If is TB "Troubleshooting"
      if (this.SAITroubleshootingMenuResult == "TB") {
        //Check if the current equipment is RUNNING
        let OTETagRunning: boolean = await this.OPCClient();
        if (OTETagRunning)
        {
          this.loading = false;
          this.messages.push({ user: 'Server', text: 'The equipment is running!', showFormattedContent: false, formattedContent: '' });
        }
        //If the equipment is STOPPED execute TROUBLESHOOTING
        else
        {
          await this.getTroubleshootingResult(this.globalOTETag);
          //await this.SAITroubleshootingChatResult();
          await this.SAITroubleshootingCodeExplainer();
          this.SAITroubleshootingConsolidatedResult();
        }

      }
      //If is CE "CodeExplainer"
      else
      {
        await this.SAITroubleshootingCodeExplainer();
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
      this.loading = false;
      alert('Failed to fetch getTroubleshootingResult.');
    }
  }

  SAITroubleshootingChatResult() {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingChatResult?ogTag=${encodeURIComponent(this.globalOTETag)}&allWrongTags=${encodeURIComponent(this.troubleshootingResultList)}&msgInput=${encodeURIComponent(this.textMessage)}`;
    this.http.post<string>(url, null, { responseType: 'text' as 'json' }).subscribe(
      async (data) => {
        // If the count is greater than 30, push to messages
        this.troubleshootingResultMessage = data || '';
        //this.messages.push({ user: 'Server', text: this.troubleshootingResultMessage, showFormattedContent: false, formattedContent: '' });
        this.messages.push({ user: 'Server', text: '', showFormattedContent: true, formattedContent: this.troubleshootingResultMessage });
        console.log('Response:', data);
        this.loading = false;
      },
      (error) => {
        console.error(error);
        this.loading = false;
        alert('Failed to fetch SAITroubleshootingChatResult.');
      }
    );
  }

  async SAITroubleshootingCodeExplainer() {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingCodeExplainer?tagName=${encodeURIComponent(this.globalOTETag)}&msgInput=${encodeURIComponent(this.textMessage)}`;
    try {
        const data = await lastValueFrom(this.http.post<string>(url, null, { responseType: 'text' as 'json' }));
        this.SAITroubleshootingCodeExplainerResult = data || '';
        //this.messages.push({ user: 'Server', text: '', showFormattedContent: true, formattedContent: this.SAITroubleshootingCodeExplainerResult });
      } catch (error) {
        console.error(error);
        alert('Failed to fetch SAITroubleshootingCodeExplainer.');
      }
  }

  SAITroubleshootingConsolidatedResult() {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingConsolidatedResult?majortags=${encodeURIComponent(this.troubleshootingResultList)}&quertag=${encodeURIComponent(this.globalOTETag)}&text=${encodeURIComponent(this.SAITroubleshootingCodeExplainerResult)}`;
    this.http.post<string>(url, null, { responseType: 'text' as 'json' }).subscribe(
      async (data) => {
        // If the count is greater than 30, push to messages
        this.troubleshootingConsolidatedResult = data || '';
        //this.messages.push({ user: 'Server', text: this.troubleshootingResultMessage, showFormattedContent: false, formattedContent: '' });
        this.messages.push({ user: 'Server', text: '', showFormattedContent: true, formattedContent: this.troubleshootingConsolidatedResult });
        console.log('Response:', data);
        this.loading = false;
      },
      (error) => {
        console.error(error);
        this.loading = false;
        alert('Failed to fetch SAITroubleshootingConsolidatedResult.');
      }
    );
  }

  //SAITroubleshootingMenu
  async SAITroubleshootingMenu() {
    const url = `https://localhost:7070/api/Troubleshooting/SAITroubleshootingMenu?msgInput=${encodeURIComponent(this.textMessage)}`;
    try {
      const data = await lastValueFrom(this.http.post<{ result1: string, result2: string }>(url, null));
      // Handle the response data as needed
      console.log('Response:', data);
      this.globalOTETag = data.result1 || '';
      this.SAITroubleshootingMenuResult = data.result2 || ''
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

﻿using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using System.Xml.Linq;
using System.Text;

using TS = SAI_OT_Apps.Server.Libraries.TroubleshootingLibrary;


namespace SAI_OT_Apps.Server.Services

{
    public class TroubleshootingService
    {
        private readonly string _apiKey;

        public TroubleshootingService(IConfiguration configuration)
        {
            _apiKey = configuration["apiKey"];
        }
        public async Task<Dictionary<string, object>> TroubleshootingProgram(string OTETagName)
        {
            Dictionary<string, object> troubleshootingResult = new Dictionary<string, object>();
            string xmlFilePath = @"C:\Users\jbonicen\OneDrive - Stefanini\Documents\SAI\SAITroubleshooting\PLC_M45_1525_Dev.L5X";

            try
            {
                // Execute troubleshooting main code
                troubleshootingResult = await TS.Troubleshooting(xmlFilePath, OTETagName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error while executing the TroubleshootingProgram: {ex.Message}");
            }

            return troubleshootingResult;
        }
        public async Task<List<string>> TroubleshootingProgram__(string OTETag)
        {
            List<string> allWrongTagsFormatted = new List<string>();
            try
            {
                List<string> allWrongTags = await RungExtractor(OTETag); // Lista com todas as tags que falharam.
                Console.WriteLine("\n");
                if (allWrongTags == null) // Error, the list is null.
                { }
                else
                {
                    allWrongTagsFormatted = allWrongTags.Distinct().ToList(); // List of failed tags formatted (Remove repeated tags).
                    string finalResult = String.Join(", ", allWrongTagsFormatted);
                    //string finalAnswer = await APIOutro(OTEEntry, finalResult);
                }
            }
            catch (Exception ex) // Caso dê erros.
            {
                Console.Error.WriteLine("Ocorreu um erro: " + ex.Message);
            }

            return allWrongTagsFormatted;

        } // End Main

        // Função recursiva.  
        async Task<List<string>> RungExtractor(string OTELine)
        {
            try
            {
                XmlDocument Xdoc = new XmlDocument();
                Xdoc.Load(@"C:\Users\jbonicen\OneDrive - Stefanini\Documents\SAI\SAITroubleshooting\PLC_M45_1525_Dev.L5X"); // Load XML File. (In this case, write the correct file path from your computer).
                XmlElement root = Xdoc.DocumentElement;

                XmlNodeList nodes = root.SelectNodes("//*[contains(text(), 'OTE(" + OTELine + ")')]"); // Extract the RUNG containing the specified OTE

                // Checks if the node was found. (Both cases return null)
                if (nodes.Count == 0) // Not found.
                {
                    Console.WriteLine("No nodes were found with the term");
                    return null;
                }

                if (nodes.Count > 1) // More than one found.
                {
                    Console.WriteLine("More than one node was found with the specified term");
                    return null;
                }

                // Preparing to create RUNG as a string.
                String rung = "";
                foreach (XmlNode node in nodes)
                {
                    rung += node.InnerXml;
                }

                // WriteLine para teste.
                Console.WriteLine("Rung XML: ");

                String formattedLine = rung.Substring(9, rung.Length - 12); // Format the RUNG to show only TAGs and expressions
                Console.WriteLine(formattedLine);

                List<string> extractedTags = ExtractTagsBeforeOTE(formattedLine); // Calls the function that extracts all the TAGs from the RUNG (OTE tag is not extracted, as it is irrelevant to the rest of the code).
                String tagsFromClient = await TroubleshootingService.OPCClient(extractedTags); // Calls the function that connects to the server and extracts the values of the tags passed in.
                Console.WriteLine(tagsFromClient);
                if (tagsFromClient == null) // If the server does not return any values.
                {
                    return null;
                }
                string apiRequestResponse = await APIRequest(_apiKey, formattedLine, tagsFromClient); // Calls the function that calls SAI-APP (main logic solver).
                if (apiRequestResponse == "")
                {
                    return new List<string>();
                }
                String[] splittedTags = apiRequestResponse.Split(new string[] { "," }, StringSplitOptions.None); // SAI returns the tags separated by commas, then split them (If more than one tag in returned from apiRequestResponse). 
                List<string> allWrongTags = new List<string>(); // An empty list that stores the tags that failed.
                Console.WriteLine("----------- from:" + OTELine);
                foreach (string strAux in splittedTags) // Print the tags that failed.
                {
                    Console.WriteLine(strAux);
                }
                Console.WriteLine("-----------");
                foreach (string str in splittedTags)
                {
                    if (str == OTELine)
                    { // If, in the RUNG, OTE tag and any expression tag is the same, this tag is not added into the list.
                        continue;
                    }
                    List<string> auxList = await RungExtractor(str); // It recursively calls the function. That is, it passes the result of the failed tags to this function again until it finds the root of the problem. 
                    if (auxList != null)
                    {
                        foreach (string str1 in auxList)
                        {
                            allWrongTags.Add(str1); // Adds tags to the list.
                        }
                    }
                    else // If the problem persists for more than one rung, this else points to the tags of the root of the problem, i.e. the last rung found.
                    {
                        allWrongTags.Add(str);
                    }
                }
                return allWrongTags;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Ocorreu um erro");
                return null;
            }
        }

        // Call API SAI-APP
        async static Task<string> APIRequest(String apiKey, String ll, String paramet)
        {
            //var apiKey = _apiKey;

            var rung = ll;
            var values = paramet;

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/66d0c7794c4a940c6c383c2f/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["rung"] = rung,
                        ["values"] = values,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string resultAPI = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                string resultAPIForma = resultAPI.Replace("output_result", "").Replace("\"", "").Replace("=", "").Replace(" ", "");
                if (resultAPIForma.ToUpper() == "NONE")
                {
                    resultAPIForma = "";
                }
                return resultAPIForma;
            }
            else
            {
                throw new Exception("API Request Error - response.IsSuccessful is not true!");
            }
        }

        //OPC UA Client - Based a tag list, connect to OPC UA Server and read the current values
        public async static Task<string> OPCClient(List<string> tagList)
        {
            Dictionary<string, object> tagValues = new Dictionary<string, object>();
            string concatTagValues = "";

            //Read the tag values
            tagValues = await TS.OPCClient(tagList);

            //Build the tag list text (tag=value)
            if (tagValues != null)
            {
                foreach (var (tag, value) in tagValues.ToList())
                {
                    concatTagValues += $"{tag}={value}\n";

                }
            }

            return concatTagValues;
        }

        // Método que extrai as TAGs do OTE.
        static List<string> ExtractTagsBeforeOTE(string line)
        {
            // Find out the position of the OTE.
            int posOte = line.IndexOf("OTE");

            // If the OTE is not found, it returns an empty list.
            if (posOte == -1)
            {
                return new List<string>();
            }

            // Extract the parts of the line before the OTE.
            string partBeforeOte = line.Substring(0, posOte);

            // Use a regular expression to find all tags within parentheses
            MatchCollection matches = Regex.Matches(partBeforeOte, @"\((.*?)\)");

            List<string> tags = new List<string>();
            foreach (Match match in matches)
            {
                tags.Add(match.Groups[1].Value);
            }

            return tags; ;
        }

        // Function API-Intro
        public async Task<String> SAITroubleshootingChatRequest(String inp)
        {
            var apiKey = _apiKey;
            var msgInput = inp;

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/66d619fa6819947d77dfb407/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["msgInput"] = msgInput,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string responseAPIINtro = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                Console.WriteLine(responseAPIINtro);
                return responseAPIINtro;
            }
            else
            {
                throw new Exception("API Intro failed!");
            }
        }

        public async Task<String> SAITroubleshootingChatResult(string ogTag, string allWrongTags, string msgInput)
        {
            var apiKey = _apiKey;

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/66d75d18516480c9c41bd3e7/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["allWrongTags"] = allWrongTags,
                        ["ogTag"] = ogTag,
                        ["msgInput"] = msgInput,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string responseAPIOutro = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                return responseAPIOutro;
            }
            else
            {
                throw new Exception("API Outro failed!");
            }
        }

        //Start here functions related to the Troubleshooting Code Explainer
        public async Task<String> SAITroubleshootingCodeExplainer(string tagName, string msgInput)
        {
           var apiKey = _apiKey; // TODO: Replace with your API key
           string plcCode = troubleshootingCodeExplainerPLCExtractor(tagName);
           Console.WriteLine(plcCode);
           string searchString = "SpeedCheck";
           bool containsText = tagName.Contains(searchString);
           if (containsText)
           {
                tagName = tagName.Replace("SpeedCheck.", "");
           }

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/66d9d83d1a65ee616e160605/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["plc code"] = plcCode,
                        ["tag name"] = tagName,
                        ["msgInput"] = msgInput,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string responseTroubleshootingCodeExaplainer = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                return responseTroubleshootingCodeExaplainer;
            }
            else
            {
                throw new Exception("API SAITroubleshootingCodeExplainer failed!");
            }
        }

        public async Task<String> SAITroubleshootingConsolidatedResult(string majortags, string quertag, string text)
        {
            var apiKey = _apiKey;

            //majortags = "{\"SafeSpeed_On\":{\"DirectReasons\":[[\"CB_Safety_DISABLE_Cmd\"]],\"IndirectReasons\":{\"CB_Safety_DISABLE_Cmd\":{\"DirectReasons\":[[\"S_SafeSpeed_On_Status\",\"SMap_LineRun_SpeedRamped\",\"SMap_LW_LineRun_SpeedRamped\",\"S_SafeSpeed_Ref\"]],\"IndirectReasons\":{}}}}}";
            //quertag = "SafeSpeed_On";
            var request = new RestRequest("api/templates/6762ef957d215ce70cb58790/execute", Method.Post); //ORIGINAL
            string searchString = "SpeedCheck";
            string searchString1 = "INFEED_BACK_UP_BIT";
            bool containsText = (quertag.Contains(searchString) || quertag.Contains(searchString1)) ;
            if (containsText)
            {
                quertag = quertag.Replace("SpeedCheck.", "");
                request = new RestRequest("api/templates/6764c9ca5f57a2b240c8d307/execute", Method.Post); //ALTERADA PARA AOI
            }

               var client = new RestClient("https://sai-library.saiapplications.com");
               request
               .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["majortags"] = majortags,
                        ["quertag"] = quertag,
                        ["text"] = text,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string responseConsolidatedResult = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                return responseConsolidatedResult;
            }
            else
            {
                throw new Exception("API Outro failed!");
            }
        }

        static string troubleshootingCodeExplainerPLCExtractor(string TagName)
        {
            try
            {
                string XmlFilePath = @"C:\Users\jbonicen\OneDrive - Stefanini\Documents\SAI\SAITroubleshooting\PLC_M45_1525_Dev.L5X";
                string searchString = "SpeedCheck";
                bool containsText = TagName.Contains(searchString);
                if (containsText)
                {
                    XmlFilePath = @"C:\Users\jbonicen\OneDrive - Stefanini\Documents\SAI\SAITroubleshooting\SpeedCheck.XML";
                    TagName = TagName.Replace("SpeedCheck.", "");
                }

                // Find and process the first rung containing the OTE or OTL for the user input tag
                string firstRung = FindFirstRung(TagName, XmlFilePath);
                //***Replace tags
                if (containsText)
                {
                    Dictionary<string, string> tags = new Dictionary<string, string>
                    {
                        { "ZeroSpeed_Bit", "SMap_LIneRun_ZeroSpeed"},
                        { "SpeedEnable_Bit","SMap_LIneRun_SpeedEnable"},
                        { "SafeSpeed_Status","S_SafeSpeed_On_Status"},
                        { "LSpdRamped","SMap_LineRun_SpeedRamped"},
                        { "Hold_Req5","hold_Req_TMP"},
                        { "Hold_Req4", "hold_Req_TMP"},
                        { "Hold_Req3", "hold_Req_TMP"},
                        { "Hold_Req2",  "S_SLC_SafeSpeedHold"},
                        { "Hold_Req1",  "S_SLS_SafeSpeedHold"},
                        { "DriversEnable_Bit",  "SMap_LineRun_DrivesEnable"},
                        { "CB_DISABLE_Cmd",   "CB_Safety_DISABLE_Cmd"},
                    };

                    foreach (var tag in tags)
                    {
                        if (firstRung.Contains(tag.Key))
                        {
                            firstRung = firstRung.Replace(tag.Key, tag.Value);
                        }
                    }
                    XmlFilePath = @"C:\Users\jbonicen\OneDrive - Stefanini\Documents\SAI\SAITroubleshooting\PLC_M45_1525_Dev.L5X";
                }

                // Collect all rungs starting with the first one
                HashSet<string> allRungsXml = new HashSet<string> { firstRung };
                Queue<string> tagsToProcess = new Queue<string>(ExtractUniqueTags(firstRung, TagName));
 
                // Process each tag to find all related rungs
                while (tagsToProcess.Count > 0)
                {
                    string currentTag = tagsToProcess.Dequeue();
                    List<string> foundRungs = FindOteOtlOtuRungsForTag(currentTag, XmlFilePath);
 
                    foreach (var rung in foundRungs)
                    {
                        if (allRungsXml.Add(rung)) // If the rung is new, process it
                        {
                            List<string> newTags = ExtractUniqueTags(rung, currentTag);
                            foreach (var newTag in newTags)
                            {
                                if (!tagsToProcess.Contains(newTag)) // Avoid re-processing the same tag
                                {
                                    tagsToProcess.Enqueue(newTag);
                                }
                            }
                        }
                    }
                }
 
                // Output the result as a single XML text
                string resultXml = string.Join(Environment.NewLine, allRungsXml);
                string SAITroubleshootingCodeExplainerResult = resultXml;

                return SAITroubleshootingCodeExplainerResult;

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("An error has occurred: " + ex.Message);
                return "";
            }

        }

        static string FindFirstRung(string tagName, string XmlFilePath)
        {
            try
            {
                XmlDocument Xdoc = new XmlDocument();
                Xdoc.Load(XmlFilePath);
                XmlElement root = Xdoc.DocumentElement;

                XmlNode node = root.SelectSingleNode("//*[contains(text(), 'OTE(" + tagName + ")') or contains(text(), 'OTL(" + tagName + ")')]");
                return node?.OuterXml;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error reading XML: " + ex.Message);
                return null;
            }
        }

        static List<string> ExtractUniqueTags(string rung, string userInput)
        {
            // Use a regular expression to match tags within parentheses
            MatchCollection matches = Regex.Matches(rung, @"\((.*?)\)");
            List<string> tags = new List<string>();

            foreach (Match match in matches)
            {
                string tag = match.Groups[1].Value;
                if (!tags.Contains(tag) && tag != userInput)
                {
                    tags.Add(tag);
                }
            }
            return tags;
        }

        static List<string> FindOteOtlOtuRungsForTag(string tagName, string XmlFilePath)
        {
            try
            {
                XmlDocument Xdoc = new XmlDocument();
                Xdoc.Load(XmlFilePath);
                XmlElement root = Xdoc.DocumentElement;

                XmlNodeList nodes = root.SelectNodes("//*[contains(text(), 'OTE(" + tagName + ")') or contains(text(), 'OTL(" + tagName + ")') or contains(text(), 'OTU(" + tagName + ")')]");

                List<string> rungs = new List<string>();
                foreach (XmlNode node in nodes)
                {
                    rungs.Add(node.OuterXml);
                }

                return rungs;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error reading XML: " + ex.Message);
                return new List<string>();
            }
        }

        public async Task<(string, string)> SAITroubleshootingMenu(string msgInput)
        {
            var apiKey = _apiKey; // TODO: Replace with your API key

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/66e9bcb8e34dad72070aa165/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["msgInput"] = msgInput,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string responseTroubleshootingMenu = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                var splitResult = responseTroubleshootingMenu.Split(';');
                if (splitResult.Length == 2)
                {
                    return (splitResult[0], splitResult[1]);
                }
                else
                {
                    throw new Exception("Unexpected response format!");
                }
            }
            else
            {
                throw new Exception("API SAITroubleshootingMenu failed!");
            }

        }


    }
}

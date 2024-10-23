using Opc.Ua;
using Org.BouncyCastle.Asn1.Ocsp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net.Http;
using System.Threading.Tasks;


namespace SAI_OT_Apps.Server.Services
{
    public class RoutineInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public XmlNode XmlNode { get; set; }
    }

    public class RungDescription
    {
        public string rungNumber { get; set; }
        public string description { get; set; }
    }
    public class CodeAuditorService
    {

        public List<string> RoutinesList(string PLCfilePath)
        {
            List<string> routineNames = new List<string>();
            try 
            { 
                if (string.IsNullOrEmpty(PLCfilePath))
                {
                    throw new ArgumentException("File path cannot be null or empty.", nameof(PLCfilePath));
                }

                if (!File.Exists(PLCfilePath))
                {
                    throw new FileNotFoundException("The specified file was not found.", PLCfilePath);
                }
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(PLCfilePath);

                XmlNodeList programNodes = xmlDoc.SelectNodes("//Programs/Program");
                List<RoutineInfo> routines = new List<RoutineInfo>();
                int routineCounter = 0;

                foreach (XmlNode programNode in programNodes)
                {
                    XmlNodeList routineNodes = programNode.SelectNodes("Routines/Routine");
                    foreach (XmlNode routineNode in routineNodes)
                    {
                        string name = routineNode.Attributes["Name"].Value;
                        string type = routineNode.Attributes["Type"].Value;
                        routines.Add(new RoutineInfo { Id = routineCounter, Name = name, Type = type, XmlNode = routineNode });
                        routineCounter++;
                    }
                }

                routineNames = routines.Select(r => r.Name).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                // Optionally, log the exception or rethrow it
            }

                return routineNames;
        }

        public string GetRoutineByName(string PLCfilePath, string routineName)
        {
            string xmlFilePath = PLCfilePath;
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlFilePath);

            XmlNode routineNode = xmlDoc.SelectSingleNode($"//Routine[@Name='{routineName}']");

            if (routineNode != null)
            {
                XmlNodeList rungNodes = routineNode.SelectNodes("RLLContent/Rung");
                List<string> rungData = new List<string>();

                foreach (XmlNode rungNode in rungNodes)
                {
                    string number = rungNode.Attributes["Number"]?.Value;
                    string type = rungNode.Attributes["Type"]?.Value;
                    string text = rungNode.SelectSingleNode("Text")?.InnerText.Trim();
                    string comment = rungNode.SelectSingleNode("Comment")?.InnerText.Trim();

                    string rungXml = $"<Rung Number=\"{number}\" Type=\"{type}\">\n" +
                                     $"<Text>\n<![CDATA[{text}]]>\n</Text>\n" +
                                     $"<Comment>\n<![CDATA[{comment}]]>\n</Comment>\n</Rung>\n";
                    rungData.Add(rungXml);
                }

                return string.Join("\n", rungData);
            }

            return null;
        }

        public string UpdateRoutineWithComments(string PLCfilePath, string routineName, List<RungDescription> RoutineDescriptionRevised)
        {
            // Create a new file path based on the original file path
            string newFilePath = Path.Combine(Path.GetDirectoryName(PLCfilePath),
                                              Path.GetFileNameWithoutExtension(PLCfilePath) + "_Commented" +
                                              Path.GetExtension(PLCfilePath));

            // Copy the contents of the original file to the new file
            File.Copy(PLCfilePath, newFilePath, true);

            // Load the new file into the XmlDocument object
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(newFilePath);

            XmlNode routineNode = xmlDoc.SelectSingleNode($"//Routine[@Name='{routineName}']");

            if (routineNode != null)
            {
                XmlNodeList rungNodes = routineNode.SelectNodes("RLLContent/Rung");

                foreach (XmlNode rungNode in rungNodes)
                {
                    XmlNode commentNode = rungNode.SelectSingleNode("Comment");
                    if (commentNode != null && commentNode.InnerText == "SKIP")
                    {
                        continue; // Skip to the next element in the foreach loop
                    }

                    string number = rungNode.Attributes["Number"]?.Value;
                    RungDescription revisedRung = RoutineDescriptionRevised.FirstOrDefault(r => Regex.Match(r.rungNumber, @"\d+").Value == number);

                    if (revisedRung != null)
                    {
                        if (commentNode != null)
                        {
                            commentNode.InnerXml = $"<![CDATA[{revisedRung.description}]]>";
                        }
                        else
                        {
                            XmlElement newCommentNode = xmlDoc.CreateElement("Comment");
                            newCommentNode.InnerXml = $"<![CDATA[{revisedRung.description}]]>";

                            XmlNode textNode = rungNode.SelectSingleNode("Text");
                            if (textNode != null)
                            {
                                rungNode.InsertBefore(newCommentNode, textNode);
                            }
                            else
                            {
                                rungNode.AppendChild(newCommentNode);
                            }
                        }
                    }
                }
                // Save the changes to the new file
                xmlDoc.Save(newFilePath);
                return "Routine updated successfully.";
            }

            return "Routine not found.";
        }

        public async Task<(string, List<Dictionary<string, string>>)> SAIDescriptionAnalysis(string routineCode)
        {

            List<Dictionary<string, string>> rungs = new List<Dictionary<string, string>>();
            string preRungAnalysis = "";

            var apiKey = "ePKQt7G7ZEiC2utRNRuW4Q"; // TODO: Replace with your API key

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/66f5710baa06172d8d5838c8/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["input"] = routineCode,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string SAIDescriptionAnalysisResult = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                rungs = ParseRungAnalysis(SAIDescriptionAnalysisResult, out preRungAnalysis);
                return (preRungAnalysis, rungs);
            }
            else
            {
                throw new Exception("API SAINetworkAnalysis failed!");
            }           


        }

        static List<Dictionary<string, string>> ParseRungAnalysis(string input, out string preRungAnalysis)
        {
            var rungs = new List<Dictionary<string, string>>();
            var lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool isRungAnalysis = false;
            Dictionary<string, string> currentRung = null;
            var preRungAnalysisBuilder = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                //if (line.Contains("Rung by Rung analysis:"))
                if (line.IndexOf("Rung by Rung analysis:", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isRungAnalysis = true;
                    continue;
                }

                if (isRungAnalysis)
                {
                    if (line.StartsWith("**Rung"))
                    {
                        if (currentRung != null)
                        {
                            rungs.Add(currentRung);
                        }
                        currentRung = new Dictionary<string, string>
                    {
                        { "Rung", line.Trim().Replace("*", "") }
                    };
                    }
                    else if (line.StartsWith("- **"))
                    {
                        var parts = line.Split(new[] { "**" }, StringSplitOptions.None);
                        if (parts.Length >= 3)
                        {
                            var key = parts[1].Trim(':', ' ');
                            var value = parts[2].Trim();
                            if (key == "Comment" || key == "Logic" || key == "Mistake" || key == "Suggestion")
                            {
                                currentRung[key] = value;
                            }
                        }
                    }
                }
                else
                {
                    preRungAnalysisBuilder.AppendLine(line);
                }
            }

            if (currentRung != null)
            {
                rungs.Add(currentRung);
            }

            preRungAnalysis = preRungAnalysisBuilder.ToString().Trim();
            return rungs;
        }

    }

    public class CodeAuditorServiceUDT
    {
        public async Task<string> AuditUDTCode(string PLCfilePath)
        {
            string SAIUDTAnalysisResult = "";
            string StandardUDTPath = @"C:\SAI\SAICodeAuditor\SAI_Auditor_EQP.L5X";
            string ProgramUDTPath = @"C:\SAI\SAICodeAuditor\SAI_Auditor_EQP.L5X";

            List<string> equipmentVariations = new List<string> { "EQP", "EQP", "EQUIPMENT", "EQUIP" };
            List<string> commandVariations = new List<string> { "CMD", "COMMAND", "COMAND", "COMMAN" };


            List<string> StandardUDTList = new List<string>();
            List<string> ProgramUDTList = new List<string>();

            StandardUDTList = GetUDTinPLCBackup(StandardUDTPath);
            ProgramUDTList = GetUDTinPLCBackup(ProgramUDTPath);

            //Run the UDT List in the Standard Program
            foreach (string StandardUDTItem in StandardUDTList)
            {
                string pattern = "Name=\"(.*?)\"";
                Match matchStandard = Regex.Match(StandardUDTItem, pattern);

                if (matchStandard.Success)
                {
                    string StandardUDTItemName = matchStandard.Groups[1].Value;
                    if(StandardUDTItemName == "IHM_EQP" || StandardUDTItemName == "IHM_CMD")
                    {
                        //Run the UDT List in the Selected Program
                        foreach (string ProgramUDTItem in ProgramUDTList)
                        {
                            Match matchProgram = Regex.Match(ProgramUDTItem, pattern);
                            if (matchStandard.Success)
                            {
                                string ProgramUDTItemName = matchProgram.Groups[1].Value;
                                bool belongToList = false;
                                if (StandardUDTItemName == "IHM_EQP")
                                {
                                    belongToList = belongsToVariations(StandardUDTItemName, equipmentVariations);
                                }
                                else
                                {
                                    belongToList = belongsToVariations(StandardUDTItemName, commandVariations);
                                }
                                //If the UDT was equal or similar go to SAI Analysis
                                if (belongToList)
                                {
                                    SAIUDTAnalysisResult = await SAICompareUDT(StandardUDTItem, ProgramUDTItem);


                                }
                            }
                        }

                    }

                }
                else
                {
                    Console.WriteLine("DataType Name not found.");
                }

            }


            return SAIUDTAnalysisResult;

        }

        async Task<string> SAICompareUDT(string StandardUDT, string ProgramUDT)
        {
            var apiKey = "ePKQt7G7ZEiC2utRNRuW4Q"; // TODO: Replace with your API key

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/67056c171a5f3654aac3f6cf/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["Standard XML"] = StandardUDT,
                        ["XML to be compared"] = ProgramUDT
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string SAICompareUDTResul = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                return SAICompareUDTResul;
            }
            else
            {
                throw new Exception("API SAICompareUDT failed!");
            }
        }

        static bool belongsToVariations(string itemName, List<string> listVariations)
        {
            string upperItemName = itemName.ToUpper();
            foreach (string variation in listVariations)
            {
                if (upperItemName == variation)
                {
                    return true;
                }
            }
            return false;
        }

        static List<string> GetUDTinPLCBackup(string xmlFilePath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlFilePath);

            XmlNode dataTypesNode = xmlDoc.SelectSingleNode("//DataTypes");

            if (dataTypesNode != null)
            {
                List<string> dataTypeList = new List<string>();

                foreach (XmlNode dataTypeNode in dataTypesNode.SelectNodes("DataType[@Class='User']"))
                {
                    string dataTypeXml = dataTypeNode.OuterXml;
                    dataTypeList.Add(dataTypeXml);
                }

                return dataTypeList;
            }
            return null;
        }

    }
}

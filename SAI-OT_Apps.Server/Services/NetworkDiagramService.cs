using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Org.BouncyCastle.Asn1.Ocsp;
using RestSharp;
using Swashbuckle.AspNetCore.SwaggerGen;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace SAI_OT_Apps.Server.Services
{
    public class NetworkDiagramService
    {
        private List<string> _diagrams;
        List<List<string>> eqpDetailsList;
        Dictionary<string, List<string>> eqpConnectionsList;
        private readonly string _apiKey;
        public NetworkDiagramService()
        {
            _diagrams = new List<string>();
        }

        public NetworkDiagramService(IConfiguration configuration) 
        {
            _apiKey = configuration["apiKey"];
            Console.WriteLine("ApiKey Constructor: " + _apiKey);
        }

        public async Task<string> SAINetworkAnalysis(string tableList, string connectionList)
        {
            var apiKey = _apiKey; // TODO: Replace with your API key
            Console.WriteLine("ApiKey: " + _apiKey);

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/66e38c05487300555ac2ef0a/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["tableList"] = tableList,
                        ["connectionList"] = connectionList,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string SAINetworkAnalysisResult = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                return SAINetworkAnalysisResult;
            }
            else
            {
                throw new Exception("API SAINetworkAnalysis failed!");
            }
        }

        public (List<List<string>>, Dictionary<string, List<string>>, Dictionary<string, List<string>>) plcAnalysis(string directoryPLC)
        {
            List<List<string>> eqpDetailsList = new List<List<string>>();
            Dictionary<string, List<string>> eqpConnectionsList = new Dictionary<string, List<string>>();

            Dictionary<string, List<string>> eqpConnectionsListAllRelated = new Dictionary<string, List<string>>();
            (eqpDetailsList, eqpConnectionsList) = ReadXmlFilesInDirectory(directoryPLC);

            eqpDetailsList = sortEqpDetailsList(eqpDetailsList);
            //eqpConnectionsListAllRelated
            eqpConnectionsListAllRelated = SynchronizeConnections(eqpConnectionsList);
            //Review the eqpDetailsList adding the number of connections for each PLC
            foreach (var eqpDetail in eqpDetailsList)
            {
                int numberOfConnetions = 0;
                if (eqpConnectionsListAllRelated.ContainsKey(eqpDetail[5]))
                {
                    numberOfConnetions = eqpConnectionsListAllRelated[eqpDetail[5]].Count;
                    eqpDetail.Add($"Connections={numberOfConnetions}");
                }
            }

            return (eqpDetailsList, eqpConnectionsList, eqpConnectionsListAllRelated);
        }

        static Dictionary<string, List<string>> SynchronizeConnections(Dictionary<string, List<string>> ipConnections)
        {
            Dictionary<string, List<string>> resultFromConnections = new Dictionary<string, List<string>>(ipConnections);
            // Cria uma cópia das chaves para evitar modificação durante a iteração
            var keys = new List<string>(resultFromConnections.Keys);
            string pattern = @"(?:IP="")?(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})(?:"")?";

            foreach (var ip in keys)
            {
                Match match = Regex.Match(ip, pattern);
                string ipSTR = match.Groups["ip"].Value;
                var connections = resultFromConnections[ip];

                // Sincroniza as conexões bidirecionalmente
                foreach (var connectedIp in connections)
                {
                    Match match2 = Regex.Match(connectedIp, pattern);
                    string connectedIpSTR = match2.Groups["ip"].Value;
                    if (!resultFromConnections.ContainsKey($"IP=\"{connectedIpSTR}\""))
                    {
                        resultFromConnections[$"IP=\"{connectedIpSTR}\""] = new List<string> { ipSTR };
                    }
                    else if (!resultFromConnections[$"IP=\"{connectedIpSTR}\""].Contains(ipSTR))
                    {
                        resultFromConnections[$"IP=\"{connectedIpSTR}\""].Add(ipSTR);
                    }
                }
            }
            return resultFromConnections;
        }

        public async Task<string> generateDrawIOXML(string eqpConnectionString)
        {
            var apiKey = _apiKey; // TODO: Replace with your API key
            string xmlDiagram = null;

            try
            {
                var client = new RestClient("https://sai-library.saiapplications.com");
                var requestNetworkDiagram = new RestRequest("api/templates/66fc27d5763941747843d7d2/execute", Method.Post)
                    .AddJsonBody(new
                    {
                        inputs = new Dictionary<string, string>
                        {
                            ["input"] = eqpConnectionString,
                        }
                    })
                    .AddHeader("X-Api-Key", apiKey);

                var responseNetworkDiagram = await client.ExecuteAsync(requestNetworkDiagram);
                if (responseNetworkDiagram.IsSuccessful)
                {
                    xmlDiagram = System.Text.Json.JsonSerializer.Deserialize<string>(responseNetworkDiagram.Content);
                }
                else
                {
                    throw new Exception("Failed to generate Draw.io XML: " + responseNetworkDiagram.ErrorMessage);
                }

                // Regular expression to match XML content
                string pattern = @"(<([^>]+)>)";
                MatchCollection matches = Regex.Matches(xmlDiagram, pattern);

                // If there are no matches, return an empty string
                if (matches.Count == 0)
                {
                    return string.Empty;
                }

                // Build the XML string from matches
                xmlDiagram = string.Join("", matches);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                throw;
            }

            return xmlDiagram;
        }

        public List<List<string>> sortEqpDetailsList(List<List<string>> _diagrams)
        {

            // Sort the list by d[6] (BackupProvided) first and then by d[1] (ProcessorType)
            _diagrams = _diagrams
                .OrderBy(d => d[5].Split('=')[1] == "False") // Sort by BackupProvided
                .ThenBy(d => d[1].Split('=')[1] == "") // Then sort by ProcessorType
                .ToList();

            for (int i = 0; i < _diagrams.Count; i++)
            {
                _diagrams[i].Insert(0, $"Index={i + 1}");
            }

            return _diagrams;
        }


            public string GetGreeting()
        {
            return "Hello from NetworkDiagramService!";
        }
        public List<string> GetNetworkDiagrams()
        {
            return _diagrams;
        }
        static bool IsValidIp(string ip)
        {
            // Regular expression for validating an IPv4 address
            Regex ipv4Pattern = new Regex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");

            // Regular expression for validating an IPv6 address
            Regex ipv6Pattern = new Regex(@"^(?:[a-fA-F0-9]{1,4}:){7}[a-fA-F0-9]{1,4}$");

            return ipv4Pattern.IsMatch(ip) || ipv6Pattern.IsMatch(ip);
        }

        static (List<string>, List<List<string>>) ExtractMessageIps(XElement rootFile, List<List<string>> eqpDetails)
        {
            // Find all tags with DataType="MESSAGE"
            var tags = rootFile.Descendants().Where(e => e.Attribute("DataType")?.Value == "MESSAGE");

            List<string> ipAddresses = new List<string>();

            foreach (var tag in tags)
            {
                // Extract the ConnectionPath attribute
                List<string> parameters = new List<string>();
                var messageParameters = tag.Descendants("MessageParameters").FirstOrDefault();
                if (messageParameters != null)
                {
                    string connectionPath = messageParameters.Attribute("ConnectionPath")?.Value;
                    if (!string.IsNullOrEmpty(connectionPath))
                    {
                        // Extract the IP address from the ConnectionPath
                        string ipAddress = "";
                        ipAddress = connectionPath.Split(new[] { ", " }, StringSplitOptions.None).Last();
                        // Check if is IP or NOT
                        if (IsValidIp(ipAddress))
                        {
                            if (!ipAddresses.Contains(ipAddress))
                            {
                                ipAddresses.Add(ipAddress);
                                bool eqpExist = false;
                                foreach (var element in eqpDetails)
                                {
                                    if (element.Any(item => item.Contains($"IP=\"{ipAddress}\"")))
                                    {
                                        eqpExist = true;
                                    }
                                }

                                if (!eqpExist)
                                {
                                    parameters = new List<string>
                                {
                                    "Name=",
                                    "ProcessorType=",
                                    "MajorRev=",
                                    "ExportDate=",
                                    $"IP=\"{ipAddress}\"",
                                    "BackupProvided=False"
                                };
                                    eqpDetails.Add(parameters);
                                }
                            }
                        }
                        else
                        {
                            (ipAddress, eqpDetails) = FindModuleIp(rootFile, ipAddress, eqpDetails);
                            if (!ipAddresses.Contains(ipAddress))
                            {
                                ipAddresses.Add(ipAddress);
                            }
                        }
                    }
                }
            }

            return (ipAddresses, eqpDetails);
        }

        static (string, List<List<string>>) FindModuleIp(XElement rootFile, string ipAddress, List<List<string>> eqpDetails)
        {
            string moduleName = ipAddress;
            List<string> parameters = new List<string>();
            // Find the module with the specified name
            foreach (var module in rootFile.Descendants("Module").Where(e => e.Attribute("Name")?.Value == moduleName))
            {
                // Find the port with the IP address
                moduleName = module.Attribute("Name")?.Value;
                string processorType = module.Attribute("CatalogNumber")?.Value;
                string majorRev = module.Attribute("Major")?.Value;
                foreach (var port in module.Descendants("Port"))
                {
                    if (port.Attribute("Address") != null)
                    {
                        ipAddress = port.Attribute("Address")?.Value;
                        bool eqpExist = false;
                        foreach (var element in eqpDetails)
                        {
                            if (element.Any(item => item.Contains(ipAddress)))
                            {
                                eqpExist = true;
                            }
                        }

                        if (!eqpExist)
                        {
                            // Add EQP in the List 
                            parameters = new List<string>
                        {
                            $"Name=\"{moduleName}\"",
                            $"ProcessorType=\"{processorType}\"",
                            $"MajorRev=\"{majorRev}\"",
                            "ExportDate=",
                            $"IP=\"{ipAddress}\"",
                            "BackupProvided=False"
                        };
                            eqpDetails.Add(parameters);
                        }

                        return (ipAddress, eqpDetails);
                    }
                }
            }
            return (null, eqpDetails);
        }

        static (Dictionary<string, string>, List<List<string>>) FindAllModulesIp(XElement rootFile, List<List<string>> eqpDetails)
        {
            Dictionary<string, string> ipAddressesModules = new Dictionary<string, string>();
            // Find all modules
            foreach (var module in rootFile.Descendants("Module"))
            {
                List<string> parameters = new List<string>();
                string moduleName = module.Attribute("Name")?.Value;
                int prodType = int.Parse(module.Attribute("ProductType")?.Value);
                string processorType = module.Attribute("CatalogNumber")?.Value;
                string majorRev = module.Attribute("Major")?.Value;
                if (!string.IsNullOrEmpty(moduleName))
                {
                    // Find the port with the IP address
                    foreach (var port in module.Descendants("Port"))
                    {
                        if (port.Attribute("Address") != null && prodType == 14)
                        {
                            string currIp = port.Attribute("Address")?.Value;
                            if (IsValidIp(currIp))
                            {
                                ipAddressesModules[moduleName] = currIp;
                                parameters = new List<string>
                            {
                                $"Name=\"{moduleName}\"",
                                $"ProcessorType=\"{processorType}\"",
                                $"MajorRev=\"{majorRev}\"",
                                "ExportDate=",
                                $"IP=\"{currIp}\"",
                                "BackupProvided=False"
                            };

                                break;  // Assuming only one IP address per module
                            }
                        }
                    }
                }
                if (parameters.Count > 0)
                {
                    bool eqpExist = false;
                    foreach (var element in eqpDetails)
                    {
                        if (element.Any(item => item.Contains(parameters[4])))
                        {
                            eqpExist = true;
                        }
                    }

                    if (!eqpExist)
                    {
                        eqpDetails.Add(parameters);
                    }
                }
            }

            return (ipAddressesModules, eqpDetails);
        }

        static List<string> ExtractMainEqp(XElement rootFile)
        {
            // Extract the required attributes from the Controller element
            var controller = rootFile.Element("Controller");
            string name = controller?.Attribute("Name")?.Value;
            string processorType = controller?.Attribute("ProcessorType")?.Value;
            string majorRev = controller?.Attribute("MajorRev")?.Value;
            string exportDate = rootFile.Attribute("ExportDate")?.Value;
            string commPath = controller?.Attribute("CommPath")?.Value;

            // Extract the IP address from CommPath
            string ipAddress = commPath?.Split('\\').Last();

            // Create a list to store the extracted parameters
            List<string> parameters = new List<string>
        {
            $"Name=\"{name}\"",
            $"ProcessorType=\"{processorType}\"",
            $"MajorRev=\"{majorRev}\"",
            $"ExportDate=\"{exportDate}\"",
            $"IP=\"{ipAddress}\"",
            "BackupProvided=True"
        };
            return parameters;
        }

        static (List<List<string>>, Dictionary<string, List<string>>) ReadXmlFilesInDirectory(string directory)
        {
            List<List<string>> eqpDetails = new List<List<string>>();
            Dictionary<string, List<string>> eqpConnections = new Dictionary<string, List<string>>();

            foreach (var file in Directory.GetFiles(directory, "*.L5X", SearchOption.AllDirectories))
            {
                Console.WriteLine(file);
                // Find the main equipment data
                XElement rootFile = XElement.Load(file);
                List<string> parametersList = ExtractMainEqp(rootFile);

                //Check if current equipment IP already exist in the list and delete to include data provided by backup file
                string targetIP = parametersList[4];
                for (int i = eqpDetails.Count - 1; i >= 0; i--)
                {
                    if (eqpDetails[i].Contains(targetIP))
                    {
                        eqpDetails.RemoveAt(i);
                    }
                }
                eqpDetails.Add(parametersList);
                Console.WriteLine(parametersList[4]);

                // Find the MESSAGE equipments
                (List<string> ipAddresses, List<List<string>> updatedEqpDetails) = ExtractMessageIps(rootFile, eqpDetails);
                eqpDetails = updatedEqpDetails;

                // Find all the MODULES equipments
                (Dictionary<string, string> ipAddressesAllModules, List<List<string>> updatedEqpDetails2) = FindAllModulesIp(rootFile, eqpDetails);
                eqpDetails = updatedEqpDetails2;

                //
                foreach (var ipAddress in ipAddressesAllModules)
                {
                    if (!ipAddresses.Contains(ipAddress.Value))
                    {
                        ipAddresses.Add(ipAddress.Value);
                    }
                }

                //Generate the Equipment Connetion List (only IPs)
                if (parametersList.Count > 0)
                {
                    if (ipAddresses.Count > 0)
                    {
                        eqpConnections[parametersList[4]] = ipAddresses;
                    }
                    else
                    {
                        eqpConnections[parametersList[4]] = new List<string>();
                    }
                }
            }

            return (eqpDetails, eqpConnections);
        }
    }
}

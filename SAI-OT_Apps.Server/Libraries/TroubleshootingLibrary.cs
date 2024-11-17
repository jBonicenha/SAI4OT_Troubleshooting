using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Globalization;

namespace SAI_OT_Apps.Server.Libraries
{
    public class TroubleshootingLibrary
    {
        public static string ProcessXmlRung(string xmlRung)
        {
            var formattedChars = new List<char>();
            bool closed = false;

            for (int i = 0; i < xmlRung.Length; i++)
            {
                char c = xmlRung[i];

                if (c == ']' || c == ')')
                {
                    closed = true;
                }
                else if (closed)
                {
                    if (c != ',')
                    {
                        formattedChars.Add(' ');
                    }
                    closed = false;
                }

                formattedChars.Add(c);
            }

            return new string(formattedChars.ToArray());
        }

        public static List<Tuple<string, int>> ExtractTextInside(string text, char char1 = '(', char char2 = ')')
        {
            var stack = new Stack<char>();
            var result = new List<Tuple<string, int>>();
            var currentText = new List<char>();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == char1)
                {
                    if (stack.Count > 0)
                    {
                        currentText.Add(c);
                    }
                    stack.Push(char2);
                }
                else if (c == char2)
                {
                    stack.Pop();
                    if (stack.Count > 0)
                    {
                        currentText.Add(c);
                    }
                    else
                    {
                        result.Add(Tuple.Create(new string(currentText.ToArray()), i - currentText.Count - 1));
                        currentText.Clear();
                    }
                }
                else if (stack.Count > 0)
                {
                    currentText.Add(c);
                }
            }

            return result;
        }

        public static bool IsNumber(string s)
        {
            return double.TryParse(s, out _);
        }

        public async static Task<Dictionary<string, object>> OPCClient(List<string> tagNamesList)
        {
            //OPC configuration (by user)
            string endpointURL = "opc.tcp://192.168.226.128:4990/SAI_PLC_UAServer";
            string deviceName = "SAI_APP";
            string programPath = "Program:MainProgram."; //Add a point at the end (like Program:MainProgram.).

            string aplicationName = "SAI_APP";
            string aplicationInstanceName = "SAI_APP";
            string sessionName = "SAITrubleshooting";


            Dictionary<string, object> tagValuesDict = new Dictionary<string, object>();
            List<object> tagValuesList = new List<object>();

            var config = new ApplicationConfiguration()
            {
                ApplicationName = aplicationName,
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier()
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };

            // Validate the application configuration
            await config.Validate(ApplicationType.Client);

            // Create an OPC UA application instance.
            var application = new ApplicationInstance
            {
                ApplicationName = aplicationInstanceName,
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };
            Session session = null;

            try
            {

                // Conecta ao servidor
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                // Create a session with the serverError establishing a connection: BadNotConnected
                session = await Session.Create(config, endpoint, false, sessionName, 60000, null, null);

                // Check if the session is connected
                if (session != null && session.Connected)
                {
                    // Read the value of the node
                    for (int i = 0; i < tagNamesList.Count; i++)
                    {
                        var nodeId = new NodeId($"ns=2;s=[{deviceName}]{programPath}{tagNamesList[i]}");
                        var value = session.ReadValue(nodeId);

                        tagValuesList.Add(value.Value);
                    }
                }
                else
                {
                    //throw new ArgumentException("Age cannot be negative.");
                    throw new Exception("Not connected to the OPC server.");
                }
                //Close the session
                session.Close();

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Ocorreu um erro: " + ex.Message);
                return null;
            }
            finally
            {
                // Close the session if it was created
                session?.Close();
            }

            if (tagNamesList.Count == tagValuesList.Count)
            {
                for (int i = 0; i < tagNamesList.Count; i++)
                {
                    tagValuesDict[tagNamesList[i]] = tagValuesList[i]; // Add key-value pairs to the dictionary
                }
            }

            return tagValuesDict;
        }

        public class InstructionHandler
        {
            private static readonly Dictionary<string, Func<List<object>, bool>> instructions;

            static InstructionHandler()
            {
                instructions = new Dictionary<string, Func<List<object>, bool>>
                {
                    { "XIC", Xic },
                    { "XIO", Xio },
                    { "GRT", Grt },
                    { "LES", Les }
                };
            }

            public static List<string> GetInstructionsList()
            {
                return new List<string>(instructions.Keys);
            }

            public static bool Xic(List<object> args)
            {
                return Convert.ToBoolean(args[0]);
            }

            public static bool Xio(List<object> args)
            {
                return !Convert.ToBoolean(args[0]);
            }

            public static bool Grt(List<object> args)
            {
                Console.WriteLine($"GRT arguments: {args[0]}, {args[1]}");
                return Convert.ToDouble(args[0]) > Convert.ToDouble(args[1]);
            }

            public static bool Les(List<object> args)
            {
                Console.WriteLine($"LES arguments: {args[0]}, {args[1]}");
                return Convert.ToDouble(args[0]) < Convert.ToDouble(args[1]);
            }

            public static bool? ExecuteInstruction(string instruction, List<object> instArgs)
            {
                if (instructions.TryGetValue(instruction, out var method))
                {
                    return method(instArgs);
                }
                return null;
            }
        }

        public class Node
        {
            public int Id { get; }
            public string Plc { get; }
            public string Program { get; }
            public int Rung { get; }
            public string Name { get; }
            public List<Node> Parents { get; }
            public List<Node> Children { get; }
            public bool? Result { get; set; }

            public Node(int id, string plc, string program, int rung, string name)
            {
                Id = id;
                Plc = plc;
                Program = program;
                Rung = rung;
                Name = name;
                Parents = new List<Node>();
                Children = new List<Node>();
                Result = null;
            }

            public void AddParent(Node parent)
            {
                Parents.Add(parent);
                parent.Children.Add(this);
            }

            public void AddChild(Node child)
            {
                Children.Add(child);
                child.Parents.Add(this);
            }

            public override string ToString()
            {
                return $"Node(Id={Id}, Plc={Plc}, Program={Program}, Rung={Rung}, Name={Name})";
            }
        }

        public static List<string> GetTagNames(List<Node> nodes)
        {
            var tagNames = new List<string>();
            var instructionsList = InstructionHandler.GetInstructionsList();

            foreach (var node in nodes)
            {
                if (node.Name != "root")
                {
                    var (arguments, pos) = ExtractTextInside(node.Name, '(', ')')[0];
                    var instruction = node.Name.Substring(0, pos);

                    if (instructionsList.Contains(instruction))
                    {
                        foreach (var arg in arguments.Split(','))
                        {
                            if (!IsNumber(arg) && arg != "?")
                            {
                                tagNames.Add(arg);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No tags were retrieved in arguments(='{arguments}') because the instruction '{instruction}' don't exist in 'InstructionHandler.instructions'.");
                    }
                }
            }

            return tagNames;
        }

        public static async Task<Dictionary<string, object>> GetTagValues(List<string> tagNamesList)
        {
            //return new Dictionary<string, object>
            //{
            //    { "G_Seq_Active", false },
            //    { "G_Pump_Start", true },
            //    { "G_Pump_Auto_Start", false },
            //    { "G_Pump_AM", false },
            //    { "G_Pump_Remote", true },
            //    { "G_Pump_Fault", false },
            //    { "G_Pump_Bypass", false },
            //    { "G_Pump_Stop", false },
            //    { "G_Pump_Timer001.DN", false },
            //    { "G_Pump_Running", false }
            //};

            return await OPCClient(tagNamesList);
        }

        public static async Task GetNodeResults(List<Node> nodes)
        {
            var tagNames = GetTagNames(nodes);
            var tagValues = await GetTagValues(tagNames);

            Console.WriteLine($"\nTag values:\n");
            foreach (var (tag, value) in tagValues.ToList())
            {
                Console.WriteLine($"\t{tag}: {value}");
            }

            foreach (var currentNode in nodes)
            {
                if (currentNode.Name != "root")
                {
                    var (arguments, position) = ExtractTextInside(currentNode.Name, '(', ')')[0];
                    var instructionName = currentNode.Name.Substring(0, position);

                    var argumentValues = new List<object>();
                    foreach (var argument in arguments.Split(','))
                    {
                        if (IsNumber(argument))
                        {
                            double value = new double();

                            // Try parsing with InvariantCulture first
                            if (double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                            {
                                value = result;
                            }
                            // If that fails, try with German culture
                            else if (double.TryParse(argument, NumberStyles.Float, new CultureInfo("de-DE"), out result))
                            {
                                value = result;
                            }
                            else
                            {
                                Console.WriteLine($"The string '{value}' is not a valid number.");
                                continue;
                            }

                            argumentValues.Add(value);
                        }
                        else if (argument == "?")
                        {
                            Console.WriteLine($"Syntax error in {currentNode}.");
                            continue;
                        }
                        else
                        {
                            if (tagValues.TryGetValue(argument, out var tagValue))
                            {
                                argumentValues.Add(tagValue);
                            }
                            else
                            {
                                Console.WriteLine($"Tag '{argument}' not found in {currentNode}.");
                                continue;
                            }
                        }
                    }

                    currentNode.Result = InstructionHandler.ExecuteInstruction(instructionName, argumentValues);
                }
            }

            Console.WriteLine("\nNode results:\n");
            foreach (var currentNode in nodes)
            {
                Console.WriteLine($"{currentNode.Name}: {currentNode.Result}");
            }
        }

        public static List<Node> BuildGraphFromRung(string xmlRung, List<Node> parentNodes, List<Node> graphNodes)
        {
            // Console.WriteLine($"XML rung: {xmlRung}");

            var processedText = string.Copy(xmlRung);
            var orMatches = ExtractTextInside(xmlRung, '[', ']');

            if (orMatches.Count > 0)
            {
                foreach (var (content, pos) in orMatches)
                {
                    processedText = processedText.Replace(content, "");
                }
            }

            var instructionArguments = ExtractTextInside(xmlRung, '(', ')');

            if (instructionArguments.Count > 0)
            {
                foreach (var (content, pos) in instructionArguments)
                {
                    processedText = processedText.Replace(content, content.Replace(',', ';'));
                }
            }

            var orConditions = processedText.Split(',');

            if (orConditions.Length > 1)
            {
                var leafNodes = new List<Node>();

                foreach (var originalCondition in orConditions)
                {
                    string condition = originalCondition; // Make a copy of the original condition
                                                          // Handle matches within the OR condition
                    if (orMatches.Count > 0)
                    {
                        string[] orSubmatches = condition.Split(new[] { "[]" }, StringSplitOptions.None);

                        if (orSubmatches.Length > 1)
                        {
                            string filledCondition = orSubmatches[0]; // Start with the first part of the split condition
                            for (int index = 1; index < orSubmatches.Length; index++)
                            {
                                Tuple<string, int> firstOrMatch = orMatches[0];
                                string firstOrMatchContent = firstOrMatch.Item1;

                                filledCondition += "[" + firstOrMatchContent + "]"; // Use the first match from orMatches
                                orMatches.RemoveAt(0); // Remove the used match
                                filledCondition += orSubmatches[index]; // Add the next part of the condition
                            }

                            // Update the condition to the filled condition
                            condition = filledCondition; // Use the new filled condition
                        }
                    }

                    // Recursively search for nodes in the current condition
                    List<Node> nodes = BuildGraphFromRung(condition, parentNodes, graphNodes);
                    if (nodes != null)
                    {
                        leafNodes.AddRange(nodes); // Add found nodes to leafNodes
                    }
                }

                return leafNodes;
            }

            var andConditions = processedText.Split(' ');

            if (orMatches.Count > 0)
            {
                for (int i = andConditions.Length - 1; i >= 0; i--)
                {
                    if (andConditions[i] == "[]")
                    {
                        andConditions[i] = orMatches[orMatches.Count - 1].Item1;
                        orMatches.RemoveAt(orMatches.Count - 1);
                    }
                }
            }

            if (orMatches.Count == 0 && andConditions.Length == 1)
            {
                var condition = andConditions[0].Replace(';', ',');

                var newNode = new Node(
                    id: graphNodes.Count,
                    plc: parentNodes[0].Plc,
                    program: parentNodes[0].Program,
                    rung: parentNodes[0].Rung,
                    name: condition
                );

                foreach (var parent in parentNodes)
                {
                    newNode.AddParent(parent);
                }

                graphNodes.Add(newNode);

                return new List<Node> { newNode };
            }
            else
            {
                foreach (var condition in andConditions)
                {
                    var leafNodes = BuildGraphFromRung(condition, parentNodes, graphNodes);
                    parentNodes = leafNodes;
                }

                return parentNodes;
            }
        }

        public static List<List<int>> DfsFindPaths(Node currentNode, int targetNodeId, List<int> currentPath = null)
        {
            if (currentPath == null)
            {
                currentPath = new List<int>();
            }

            currentPath.Add(currentNode.Id);

            if (currentNode.Id == targetNodeId)
            {
                return new List<List<int>> { new List<int>(currentPath) };
            }

            var paths = new List<List<int>>();

            foreach (var child in currentNode.Children)
            {
                var newPaths = DfsFindPaths(child, targetNodeId, currentPath);
                paths.AddRange(newPaths);

                currentPath.RemoveAt(currentPath.Count - 1);
            }

            return paths;
        }

        public static List<List<Node>> CollectNodesFromPaths(List<List<int>> paths, Dictionary<int, Node> nodeGraph, bool expectedResult = false)
        {
            var collectedResults = new List<List<Node>>();

            foreach (var path in paths)
            {
                var filteredNodes = new List<Node>();

                if (expectedResult)
                {
                    foreach (var nodeID in path)
                    {
                        var node = nodeGraph[nodeID];
                        if (node.Result.HasValue)
                        {
                            if (node.Name != "root" && node.Result.Value == expectedResult)
                            {
                                filteredNodes.Add(node);
                            }
                            else
                            {
                                filteredNodes.Clear();
                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var nodeId in path)
                    {
                        var node = nodeGraph[nodeId];
                        if (node.Name != "root" && node.Result == expectedResult)
                        {
                            filteredNodes.Add(node);
                        }
                    }
                }

                if (filteredNodes.Count > 0)
                {
                    collectedResults.Add(filteredNodes);
                }
            }

            return collectedResults;
        }

        public static List<List<Node>> FilterFalseResults(List<List<Node>> pathsResults)
        {
            var pathsResultsFiltered = new List<List<Node>>(pathsResults);

            for (int i = 0; i < pathsResultsFiltered.Count; i++)
            {
                var currentResult = pathsResultsFiltered[i];

                Node lastNode = null;
                Node nextNode = null;

                for (int j = 0; j < currentResult.Count; j++)
                {
                    var currentNode = currentResult[j];

                    if (j + 1 < currentResult.Count)
                    {
                        nextNode = currentResult[j + 1];
                    }

                    for (int k = 0; k < pathsResultsFiltered.Count; k++)
                    {
                        if (k == i) continue;

                        var comparisonResult = pathsResultsFiltered[k];

                        int? firstMatchingIndex = null;
                        for (int l = 0; l < comparisonResult.Count; l++)
                        {
                            var comparisonNode = comparisonResult[l];

                            if (lastNode == null)
                            {
                                if (currentNode.Id == comparisonNode.Id)
                                {
                                    if (nextNode == null)
                                    {
                                        pathsResultsFiltered[k] = new List<Node> { comparisonResult[l] };
                                        break;
                                    }
                                    else
                                    {
                                        pathsResultsFiltered[k] = comparisonResult.GetRange(l, comparisonResult.Count - l);
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                if (firstMatchingIndex == null)
                                {
                                    if (lastNode.Id == comparisonNode.Id)
                                    {
                                        firstMatchingIndex = l;
                                    }
                                }
                                else
                                {
                                    if (currentNode.Id == comparisonNode.Id)
                                    {
                                        if (nextNode == null)
                                        {
                                            pathsResultsFiltered[k] = comparisonResult.GetRange(0, firstMatchingIndex.Value + 1)
                                                .Concat(comparisonResult.GetRange(l, comparisonResult.Count - l)).ToList();
                                        }
                                        else
                                        {
                                            pathsResultsFiltered[k] = comparisonResult.GetRange(0, firstMatchingIndex.Value + 1)
                                                .Concat(comparisonResult.GetRange(l, comparisonResult.Count - l)).ToList();
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    lastNode = currentNode;
                }
            }

            return pathsResultsFiltered;
        }

        public static async Task<List<Node>> ProcessPlcRung(string plcName, string programName, string xmlRungString)
        {
            xmlRungString = xmlRungString.Replace(" ", "");

            Console.WriteLine($"XML rung:\n{xmlRungString}");

            var formattedRung = ProcessXmlRung(xmlRungString);
            Console.WriteLine($"Rung:\n{formattedRung}");

            var rootNode = new Node(0, plcName, programName, 1, "root");

            var graphNodes = new List<Node> { rootNode };

            BuildGraphFromRung(formattedRung, new List<Node> { rootNode }, graphNodes);

            Console.WriteLine("\nNodes:\n");
            foreach (var node in graphNodes)
            {
                Console.WriteLine(node);
            }

            Console.WriteLine("\nAdjacent list:\n");
            foreach (var node in graphNodes)
            {
                Console.WriteLine($"{node.Name}: {string.Join(", ", node.Children.Select(n => n.Name))}");
            }

            await GetNodeResults(graphNodes);

            return graphNodes;
        }

        public static string findOTERung(string xmlFilePath, string tagName)
        {
            XmlDocument Xdoc = new XmlDocument();
            Xdoc.Load(xmlFilePath); // Load XML File. (In this case, write the correct file path from your computer).
            XmlElement root = Xdoc.DocumentElement;

            string xPath = $"//*[contains(text(), 'OTE({tagName})')]";
            XmlNodeList nodes = root.SelectNodes(xPath); // Extract the RUNG containing the specified OTE

            // Checks if the node was found. (Both cases return null)
            if (nodes.Count == 0) // Not found.
            {
                //Console.WriteLine("No nodes were found with the term");
                //return null;
                throw new Exception($"No nodes was found using xpath='{xPath}'.");
            }

            if (nodes.Count > 1) // More than one found.
            {
                Console.WriteLine($"More than one node was found for the xpath='{xPath}'. The last will be use.");
            }

            // Preparing to create RUNG as a string.
            String rung = "";
            foreach (XmlNode node in nodes)
            {
                rung = node.InnerXml;
            }

            rung = rung.Substring(9, rung.Length - 13); // Get the data between '<![CDATA[' and ';]]>'

            Console.WriteLine($"Rung XML: {rung}");
            return rung;

        }

        public static async Task<List<List<Node>>> TroubleshootingOTE(string xmlRungString, string targetTagName, bool expectedValue = false)
        {
            string plcName = "SAI_APP";
            string programName = "Program";

            var targetNodeName = $"OTE({targetTagName})";

            var graphNodes = await ProcessPlcRung(plcName, programName, xmlRungString);

            int? targetNodeId = null;
            foreach (var node in graphNodes)
            {
                if (node.Name == targetNodeName)
                {
                    targetNodeId = node.Id;
                    break;
                }
            }

            if (targetNodeId == null)
            {
                //Console.WriteLine($"The target node name '{targetNodeName}' was not found.");
                //return null;
                throw new Exception($"The target node name '{targetNodeName}' was not found.");
            }

            var paths = DfsFindPaths(graphNodes[0], targetNodeId.Value);

            Console.WriteLine($"\nAll paths from Node(0) to Node({targetNodeId}):\n");
            foreach (var path in paths)
            {
                Console.WriteLine(string.Join(" -> ", path.Select(id => graphNodes[id].Name)));
            }

            //var targetTagName = GetTagNames(new List<Node> { graphNodes[targetNodeId.Value] })[0];
            var filteredPaths = new List<List<int>>();

            foreach (var path in paths)
            {
                bool tagFound = false;

                foreach (var nodeId in path)
                {
                    if (nodeId > 0)
                    {
                        var node = graphNodes[nodeId];
                        string nodeTagName = "";

                        List<string> tagNames = GetTagNames(new List<Node> { node });
                        if (tagNames.Count > 0)
                        {
                            nodeTagName = tagNames[0];
                        }
                        else
                        {
                            continue;
                        }

                        if (nodeTagName == targetTagName && node.Name != targetNodeName)
                        {
                            tagFound = true;
                            break;
                        }
                    }
                }

                if (!tagFound)
                {
                    filteredPaths.Add(path);
                }
            }

            var invalidNodes = CollectNodesFromPaths(filteredPaths, graphNodes.ToDictionary(n => n.Id), expectedValue);
            Console.WriteLine($"\nNodes detection results:\n");
            foreach (var results in invalidNodes)
            {
                Console.WriteLine(string.Join(", ", results.Select(n => n.Name)));
            }

            var filteredPathsResults = FilterFalseResults(invalidNodes);
            Console.WriteLine($"\nPaths results:\n");
            foreach (var results in filteredPathsResults)
            {
                Console.WriteLine(string.Join(", ", results.Select(n => n.Name)));
            }

            var finalResults = new Dictionary<string, List<Node>>();
            foreach (var result in filteredPathsResults)
            {
                var key = string.Join("", result.Select(n => n.Id.ToString()));
                finalResults[key] = new List<Node>(result);
            }

            Console.WriteLine($"\nFinal result:\n");
            foreach (var result in finalResults.Values)
            {
                Console.WriteLine(string.Join(", ", result.Select(n => n.Name)));
            }

            //var tagResultList = new List<List<string>>();
            //foreach (var result in finalResults.Values)
            //{
            //    var tagNames = new List<string>();
            //    foreach (var node in result)
            //    {
            //        tagNames.Add(GetTagNames(new List<Node> { node })[0]);
            //    }

            //    tagResultList.Add(tagNames);
            //}

            return finalResults.Values.ToList();
        }

        public static async Task<Dictionary<string, object>> Troubleshooting(string xmlFilePath, string targetTagName, bool expectedValue = false)
        {
            Dictionary<string, object> troubleshootingResult = new Dictionary<string, object>();

            Console.WriteLine($"\nTroubleshooting: targetTagName={targetTagName}, expectedValue={expectedValue}\n");

            try
            {
                string xmlRungString = string.Empty;

                List<List<Node>> directNodesReasons = new List<List<Node>>();
                Dictionary<string, Node> searchIndirectTags = new Dictionary<string, Node>();

                List<List<string>> directReasons = new List<List<string>>();
                Dictionary<string, object> indirectReasons = new Dictionary<string, object>();

                // Get the rung which the OTE contains the tag name
                xmlRungString = findOTERung(xmlFilePath, targetTagName);

                // Execute troubleshooting main code
                directNodesReasons = await TroubleshootingOTE(xmlRungString, targetTagName, expectedValue);

                foreach (var nodesReason in directNodesReasons)
                {
                    var tagNames = new List<string>();
                    foreach (var node in nodesReason)
                    {
                        string tagName = GetTagNames(new List<Node> { node })[0];
                        tagNames.Add(tagName);
                        searchIndirectTags[tagName] = node;
                    }

                    directReasons.Add(tagNames);
                }

                foreach (var (tagName, node) in searchIndirectTags.ToList())
                {
                    Dictionary<string, object> indirectToubleshootingResult = new Dictionary<string, object>();
                    try
                    {
                        if (node.Result.HasValue)
                        {
                            var tagValues = await GetTagValues(new List<string> { tagName });
                            indirectToubleshootingResult = await Troubleshooting(xmlFilePath, tagName, (bool)tagValues[tagName]);
                        }
                    }
                    catch { continue; }

                    if (indirectToubleshootingResult.Count > 0)
                    {
                        indirectReasons[tagName] = indirectToubleshootingResult[tagName];
                    }
                }

                troubleshootingResult[targetTagName] = new Dictionary<string, object>
                {
                    { "DirectReasons", directReasons },
                    { "IndirectReasons", indirectReasons }
                };

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error while executing the Troubleshooting: {ex.Message}");
            }

            return troubleshootingResult;
        }
    }
}

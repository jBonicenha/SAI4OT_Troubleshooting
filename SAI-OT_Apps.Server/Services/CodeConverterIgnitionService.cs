using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SAI_OT_Apps.Server.Services
{
    public class CodeConverterIgnitionService
    {
        public async Task<List<string>> CodeConverterIgnitionTemplateList(string projectPath)
        {
            List<string> templateList = new List<string>();

            string xmlFile = projectPath + @"\Vision\VISION.xml";
            XDocument xmlDocument = XDocument.Load(xmlFile);

            templateList = xmlDocument.Descendants("c")
                .Where(c => (string)c.Attribute("cls") == "com.inductiveautomation.factorypmi.application.components.template.TemplateHolder")
                .Select(c => c.Descendants("str").FirstOrDefault()?.Value)
                .Where(name => name != null)
                .ToList();

            bool hasInputIcon = templateList.Any(name => name.StartsWith("InputIcon"));
            bool hasValveComponent = templateList.Any(name => name.StartsWith("V_"));


            if (hasInputIcon && hasValveComponent)
            {
                templateList = new List<string> { "InputIcon", "Valve" };
            }

            return templateList;
        }
        public async Task CodeConverterIgnitionGenerateScreen(string projectPath)
        {
            // Needed file paths (Excluding templates)
            string xmlFile = projectPath + @"\Vision\VISION.xml";
            string finalJson = projectPath + @"\Perspective\view.json";
            var folderPath = projectPath + @"\Perspective\CEP_BATCHING_SCREEN";
            var jsonFile = projectPath + @"\Perspective\view.json";
            var zipPath = projectPath + @"\Perspective\CEP_BATCHING_SCREEN.zip";

            // Calling the function to get oc(Lines) and ccommcc(Components) data.
            var (ccoomcc, oc) = ConvertToJson(xmlFile);

            // Get the templates from SVG files.
            var InputIcon = JsonRead(projectPath + @"\templatesJSON\InputIcon.JSON");
            var Valve = JsonRead(projectPath + @"\templatesJSON\V_.JSON");
            var linestemp = JsonRead(projectPath + @"\templatesJSON\LINESjson.JSON");

            var templates = new Dictionary<string, List<Dictionary<string, object>>>
            {
            { "Valve", Valve },
            { "InputIcon", InputIcon },
            { "LINES", linestemp }
            };

            // Build the final JSON
            var ccoomccData = ccoomcc;
            var lines = oc;
            var view = ModifyJsonBasedOnName(ccoomccData, templates);

            // Read VISION xml file and get all bindings.
            var xmldata = ReadXmlFile(xmlFile);
            var tagpath = bindingFinder(xmldata);

            //Add Lines, storage TANKs and Labels
            view = AddTankComponent(view, ccoomccData, tagpath);
            var view_final = AddLines(view, lines);
            var labels = ExtractLabels(xmldata);
            view_final = AddLabelComponent(view_final, labels);

            // Export the final JSON as view.json
            WriteJsonToFile(view_final, finalJson);

            //Send the final JSON to ZIP
            SendJsonToZIP(folderPath, jsonFile, zipPath);
        }
        static List<(string, string, string)> CcommExtract(string xmlFile)
        {
            var tree = XDocument.Load(xmlFile);
            var root = tree.Root;
            var extractedItems = new List<(string, string, string)>();

            foreach (var cComm in root.Descendants("c-comm"))
            {
                string componentName = null;
                string positionValue = null;
                string sizeValue = null;

                foreach (var elem in cComm.Elements())
                {
                    if (elem.Name == "str")
                    {
                        componentName = elem.Value;
                    }
                    else if (elem.Name == "r2dd")
                    {
                        positionValue = elem.Value;
                    }
                }

                if (componentName != null && positionValue != null)
                {
                    extractedItems.Add((componentName, positionValue, sizeValue));
                }
            }

            return extractedItems;
        }
        static List<(string, string)> CcExtract(string xmlFile)
        {
            var tree = XDocument.Load(xmlFile);
            var root = tree.Root;
            var extractedItems = new List<(string, string)>();

            foreach (var component in root.Descendants("c"))
            {
                var componentType = component.Attribute("cls")?.Value;

                foreach (var cc in component.Descendants("c-c"))
                {
                    var mAttr = cc.Attribute("m")?.Value;
                    var sAttr = cc.Attribute("s")?.Value;

                    if (mAttr == "setTemplatePath" && sAttr == "1;str")
                    {
                        var strElem = cc.Element("str");
                        if (strElem != null)
                        {
                            var templatePath = strElem.Value;
                            extractedItems.Add((componentType, templatePath));
                        }
                    }
                }
            }

            return extractedItems;
        }
        static List<(string, string, string, string)> OcExtract(string xmlFile)
        {
            var tree = XDocument.Load(xmlFile);
            var root = tree.Root;
            var extractedItems = new List<(string, string, string, string)>();

            foreach (var oC in root.Descendants("o-c"))
            {
                if (oC.Attribute("m")?.Value == "setTarget")
                {
                    string componentType = null;
                    foreach (var o in oC.Descendants("o"))
                    {
                        var cls = o.Attribute("cls")?.Value;
                        if (cls != null)
                        {
                            componentType = cls.Split('.').Last();
                            break;
                        }
                    }

                    string componentName = null;
                    foreach (var s in oC.Descendants("s"))
                    {
                        var name = s.Attribute("name")?.Value;
                        if (name != null)
                        {
                            componentName = name;
                            break;
                        }
                    }

                    string startPoint = null;
                    string endPoint = null;
                    foreach (var gp in oC.Descendants("gp"))
                    {
                        var pathData = gp.Value.Trim();
                        if (!string.IsNullOrEmpty(pathData))
                        {
                            var parts = pathData.Split();
                            if (parts.Length >= 5)
                            {
                                endPoint = $"{parts[2]};{parts[3]}";
                                startPoint = $"{parts[5]};{parts[6]}";
                                break;
                            }
                        }
                    }

                    if (componentType != null && componentName != null && startPoint != null && endPoint != null)
                    {
                        extractedItems.Add((componentType, componentName, startPoint, endPoint));
                    }
                }
            }

            return extractedItems;
        }
        static void SendJsonToZIP(string folderPath, string jsonFile, string zipPath)
        {
            string destinationDirectory = Path.Combine(folderPath, @"com.inductiveautomation.perspective\views\View\");
            string destinationViewJsonPath = Path.Combine(destinationDirectory, "view.json");

            Directory.CreateDirectory(Path.GetDirectoryName(destinationViewJsonPath));

            File.Copy(jsonFile, destinationViewJsonPath, true);

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(folderPath, zipPath, CompressionLevel.Fastest, false);
        }
        static void WriteJsonToFile(Dictionary<string, object> jsonData, string filename)
        {
            try
            {
                var json = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filename, json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e}");
            }
        }
        static string ReadXmlFile(string filePath)
        {
            return File.ReadAllText(filePath);
        }
        static Dictionary<string, object> TransformJson(List<Dictionary<string, string>> ccoomccData)
        {
            var transformedData = new Dictionary<string, object>
        {
             { "custom", new Dictionary<string, object>() },
             { "params", new Dictionary<string, object>() },
             { "props", new Dictionary<string, object>
                 {
                     { "defaultSize", new Dictionary<string, int>
                         {
                             { "height", 572 },
                             { "width", 1487 }
                         }
                     }
                 }
             },
             { "root", new Dictionary<string, object>
                 {
                     { "children", new List<Dictionary<string, object>>() },
                     { "meta", new Dictionary<string, string> { { "name", "root" } } },
                     { "type", "ia.container.coord" }
                 }
             }
        };

            foreach (var item in ccoomccData)
            {
                var componentName = item["Component_name"];
                var position = item["Position"].Split(';').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                var size = item["Size"].Split(';').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

                var component = new Dictionary<string, object>
                 {
                     { "meta", new Dictionary<string, string> { { "name", componentName } } },
                     { "position", new Dictionary<string, float>
                         {
                             { "x", position[0]},
                             { "y", position[1]},
                             { "width", size[0]},
                             { "height", size[1]}
                         }
                     },
                     { "props", new Dictionary<string, object>
                         {
                             { "elements", new List<Dictionary<string, object>>
                                 {
                                     new Dictionary<string, object>
                                     {
                                         { "elements", new List<Dictionary<string, object>>
                                             {
                                                 new Dictionary<string, object>
                                                 {
                                                     { "elements", new List<Dictionary<string, object>>() },
                                                     { "name", "group" },
                                                     { "stroke", new Dictionary<string, string> { { "paint", "null" } } },
                                                     { "type", "group" }
                                                 }
                                             }
                                         },
                                         { "id", "Layer_1" },
                                         { "name", "Layer_1" },
                                         { "type", "group" }
                                     }
                                 }
                             }
                         }
                     },
                     { "type", "ia.shapes.svg" }
             };

                ((List<Dictionary<string, object>>)((Dictionary<string, object>)transformedData["root"])["children"]).Add(component);
            }

            return transformedData;
        }
        static Dictionary<string, object> ModifyJsonBasedOnName(List<Dictionary<string, string>> ccommccData, Dictionary<string, List<Dictionary<string, object>>> templates)
        {
            try
            {
                var data = TransformJson(ccommccData);

                if (data.ContainsKey("root") && ((Dictionary<string, object>)data["root"]).ContainsKey("children"))
                {
                    var children = (List<Dictionary<string, object>>)((Dictionary<string, object>)data["root"])["children"];
                    foreach (var child in children)
                    {
                        if (child.ContainsKey("meta") && ((Dictionary<string, string>)child["meta"]).ContainsKey("name"))
                        {
                            var nameValue = ((Dictionary<string, string>)child["meta"])["name"];
                            List<Dictionary<string, object>> templateToAdd = new List<Dictionary<string, object>>();

                            // Console.WriteLine("Template : " + templates.GetValueOrDefault("InputIcon"));

                            if (nameValue.StartsWith("InputIcon"))
                            {
                                templateToAdd = templates.GetValueOrDefault("InputIcon");
                            }
                            else if (nameValue.StartsWith("V_"))
                            {
                                templateToAdd = templates.GetValueOrDefault("Valve");
                            }
                            //else if (nameValue.StartsWith("StorageTank"))
                            //{
                            //    templateToAdd = templates.GetValueOrDefault("StorageTank");
                            //}
                            else if (nameValue.StartsWith("M_131"))
                            {
                                templateToAdd = templates.GetValueOrDefault("WaterTank");
                            }

                            if (templateToAdd != null && child.ContainsKey("props") && ((Dictionary<string, object>)child["props"]).ContainsKey("elements"))
                            {
                                var elements = (List<Dictionary<string, object>>)((Dictionary<string, object>)child["props"])["elements"];
                                foreach (var element in elements)
                                {
                                    if (element.ContainsKey("elements"))
                                    {
                                        var subElements = (List<Dictionary<string, object>>)element["elements"];
                                        foreach (var subElement in subElements)
                                        {
                                            if (subElement.ContainsKey("elements"))
                                            {
                                                var subSubElement = (List<Dictionary<string, object>>)subElement["elements"];
                                                if (subSubElement.Count == 0)
                                                {
                                                    subElement.Clear();
                                                    if (templateToAdd is List<Dictionary<string, object>> listTemplate)
                                                    {
                                                        subElement["elements"] = new List<Dictionary<string, object>>(listTemplate);
                                                        Console.WriteLine("Elements added.");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("error");
                }
                return data;
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred: {e}");
                return null;
            }
        }
        static Dictionary<string, object> AddLabelComponent(Dictionary<string, object> view, List<Dictionary<string, object>> labelData)
        {
            if (!view.ContainsKey("root") || !((Dictionary<string, object>)view["root"]).ContainsKey("children"))
            {
                return view;
            }

            var children = (List<Dictionary<string, object>>)((Dictionary<string, object>)view["root"])["children"];

            foreach (var label in labelData)
            {
                // Extraindo os valores de "Text", "X", "Y", "Width" e "Height"
                var text = label["Text"].ToString();
                var x = Convert.ToSingle(label["X"], CultureInfo.InvariantCulture);
                var y = Convert.ToSingle(label["Y"], CultureInfo.InvariantCulture);
                var width = Convert.ToSingle(label["Width"], CultureInfo.InvariantCulture);
                var height = Convert.ToSingle(label["Height"], CultureInfo.InvariantCulture);

                // Criando um novo componente de label
                var newComponent = new Dictionary<string, object>
                {
                     { "type", "ia.display.label" },
                     { "version", 0 },
                     { "props", new Dictionary<string, object>
                         {
                             { "text", text },
                             { "textStyle", new Dictionary<string, object>
                                 {
                                     { "fontSize", 10 }
                                 }
                             }
                         }
                     },
                     { "meta", new Dictionary<string, string> { { "name", "NewLabel" } } },
                     { "position", new Dictionary<string, float>
                         {
                             { "x", x },
                             { "y", y },
                             { "width", width },
                             { "height", height }
                         }
                     },
                     { "custom", new Dictionary<string, object>() }
                };

                children.Add(newComponent);
            }

            return view;
        }
        static Dictionary<string, object> AddLines(Dictionary<string, object> view, List<Dictionary<string, string>> lineData)
        {
            if (!view.ContainsKey("root") || !((Dictionary<string, object>)view["root"]).ContainsKey("children"))
            {
                return view;
            }

            var children = (List<Dictionary<string, object>>)((Dictionary<string, object>)view["root"])["children"];

            var json = new Dictionary<string, object>
             {
                 { "type", "ia.shapes.svg" },
                 { "version", 0 },
                 { "props", new Dictionary<string, object>
                     {
                         { "elements", new List<Dictionary<string, object>>
                             {
                                 new Dictionary<string, object>
                                 {
                                     { "type", "group" },
                                     { "name", "group" },
                                     { "elements", new List<Dictionary<string, object>>() }
                                 }
                             }
                         }
                     }
                 },
                 { "meta", new Dictionary<string, object>
                     {
                         { "name", "LINES" }
                     }
                 },
                 { "position", new Dictionary<string, object>
                     {
                         { "x", -0.010419999999999874 },
                         { "y", 0.9635500000000001 },
                         { "width", 1194 },
                         { "height", 512 }
                     }
                 },
                 { "custom", new Dictionary<string, object>() }
        };

            foreach (var line in lineData)
            {
                var componentName = line["componentName"];
                var startPoint = line["startPoint"].Split(';');
                var endPoint = line["endingPoint"].Split(';');
                var x1 = startPoint[0];
                var y1 = startPoint[1];
                var x2 = endPoint[0];
                var y2 = endPoint[1];

                var newLine = new Dictionary<string, object>
                {
                     { "type", "line" },
                     { "name", componentName },
                     { "id", componentName },
                     { "x1", x1 },
                     { "y1", y1 },
                     { "x2", x2 },
                     { "y2", y2 },
                     { "fill", new Dictionary<string, string> { { "paint", "transparent" } } },
                     { "stroke", new Dictionary<string, string>
                         {
                             { "paint", "#000" },
                             { "linecap", "undefined" },
                             { "linejoin", "undefined" }
                         }
                     }
                };

                var elementsList = (List<Dictionary<string, object>>)((Dictionary<string, object>)((List<Dictionary<string, object>>)((Dictionary<string, object>)json["props"])["elements"])[0])["elements"];
                elementsList.Add(newLine);
            }

            children.Add(json);
            return view;
        }
        static Dictionary<string, object> AddTankComponent(Dictionary<string, object> view, List<Dictionary<string, string>> componentData, Dictionary<string, string> tagPaths)
        {
            if (!view.ContainsKey("root") || !((Dictionary<string, object>)view["root"]).ContainsKey("children"))
            {
                return view;
            }

            var children = (List<Dictionary<string, object>>)((Dictionary<string, object>)view["root"])["children"];

            foreach (var component in componentData.Where(cd => cd["Component_name"].StartsWith("StorageTank")))
            {
                var componentName = component["Component_name"];

                var positionParts = component["Position"].Split(';').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                var sizeParts = component["Size"].Split(';').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

                string tagPath = tagPaths.ContainsKey(componentName) ? tagPaths[componentName] : null;

                var newComponent = new Dictionary<string, object>
                 {
                     { "type", "ia.display.cylindrical-tank" },
                     { "version", 0 },
                     { "props", new Dictionary<string, object>() },
                     { "meta", new Dictionary<string, string> { { "name", componentName } } },
                     { "position", new Dictionary<string, float>
                         {
                             { "x", positionParts[0] },
                             { "y", positionParts[1] },
                             { "width", sizeParts[0] },
                             { "height", sizeParts[1] }
                         }
                     },
                     { "custom", new Dictionary<string, object>() },
                     { "propConfig", new Dictionary<string, object>
                         {
                             { "props.value", new Dictionary<string, object>
                                 {
                                     { "binding", new Dictionary<string, object>
                                         {
                                             { "type", "tag" },
                                             { "config", new Dictionary<string, object>
                                                 {
                                                     { "mode", "direct" },
                                                     { "tagPath", tagPath ?? "[default]codeconv/Tank1_level" },
                                                     { "fallbackDelay", 2.5 }
                                                 }
                                             }
                                         }
                                     }
                                 }
                             }
                         }
                     }
                 };

                children.Add(newComponent);
            }

            return view;
        }
        static List<Dictionary<string, object>> ExtractLabels(string xml)
        {
            var document = XDocument.Parse(xml);
            var labels = new List<Dictionary<string, object>>();
            var texts = new Dictionary<string, string>();

            foreach (var label in document.Descendants("c").Where(e => (string)e.Attribute("cls") == "com.inductiveautomation.factorypmi.application.components.PMILabel"))
            {
                string textId = (string)label.Descendants("c-c").FirstOrDefault(e => (string)e.Attribute("m") == "setText")?.Element("str")?.Attribute("id");
                string text = (string)label.Descendants("c-c").FirstOrDefault(e => (string)e.Attribute("m") == "setText")?.Element("str") ??
                              (string)label.Descendants("c-c").FirstOrDefault(e => (string)e.Attribute("m") == "setText")?.Element("ref")?.Value;

                if (!string.IsNullOrEmpty(textId) && !string.IsNullOrEmpty(text))
                {
                    texts[textId] = text;
                }
                else if (!string.IsNullOrEmpty(text) && text.StartsWith("id="))
                {
                    text = texts[text.Substring(3)];
                }

                var r2dd = label.Descendants("r2dd").FirstOrDefault()?.Value.Split(';');
                if (r2dd != null && r2dd.Length == 4)
                {
                    float x = float.Parse(r2dd[0]);
                    float y = float.Parse(r2dd[1]);
                    float width = float.Parse(r2dd[2]);
                    float height = float.Parse(r2dd[3]);

                    var labelInfo = new Dictionary<string, object>
                {
                    { "Text", text },
                    { "X", x },
                    { "Y", y },
                    { "Width", width  },
                    { "Height", height }
                };

                    labels.Add(labelInfo);
                }
            }

            return labels;
        }
        static Dictionary<string, string> bindingFinder(string xmlContent)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);

            Dictionary<string, string> result = new Dictionary<string, string>();

            XmlNodeList bindingNodes = xmlDoc.SelectNodes("//o[@cls='com.inductiveautomation.factorypmi.application.binding.SimpleBoundTagAdapter']");

            foreach (XmlNode bindingNode in bindingNodes)
            {
                XmlNode tagPathNode = bindingNode.SelectSingleNode(".//o-c[@m='setTagPathString']/str");
                string tagPath = tagPathNode != null ? tagPathNode.InnerText : null;

                XmlNode componentNameNode = bindingNode.SelectSingleNode(".//o-c[@m='setTarget']/c/c-comm/str");
                string componentName = componentNameNode != null ? componentNameNode.InnerText : null;

                if (componentName != null && tagPath != null)
                {
                    result[componentName] = tagPath.Replace("client", "default");
                }
            }

            return result;
        }
        static List<Dictionary<string, object>> JsonRead(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                if (json.Trim().StartsWith("["))
                {
                    return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                }
                else
                {
                    List<Dictionary<string, object>> resultList = new List<Dictionary<string, object>>();
                    resultList.Add(JsonConvert.DeserializeObject<Dictionary<string, object>>(json));

                    return resultList;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e}");
                return null;
            }
        }
        public static (List<Dictionary<string, string>>, List<Dictionary<string, string>>) ConvertToJson(string xmlFile)
        {
            var ccommData = CcommExtract(xmlFile);
            var ccData = CcExtract(xmlFile);
            var ocData = OcExtract(xmlFile);

            var ccommccList = new List<Dictionary<string, string>>();
            var ocList = new List<Dictionary<string, string>>();

            var maxLength = Math.Max(ccommData.Count, Math.Max(ccData.Count, ocData.Count));
            for (int i = 0; i < maxLength; i++)
            {
                var (componentName, positionValue, ccommSize) = i < ccommData.Count ? ccommData[i] : (null, null, null);
                var (componentType, ccTemplatePath) = i < ccData.Count ? ccData[i] : (null, null);
                var (ocComponentType, ocComponentName, ocStart, ocEnd) = i < ocData.Count ? ocData[i] : (null, null, null, null);

                var positionParts = positionValue?.Split(';') ?? new string[0];

                //if (componentType != null)
                //{
                //    componentType = componentType.Split('.').Last();
                //}

                string position = positionParts.Length >= 4 ? string.Join(";", positionParts.Take(2)) : positionValue ?? "";
                string size = positionParts.Length >= 4 ? string.Join(";", positionParts.Skip(2)) : ccommSize ?? "";

                var currentObj = new Dictionary<string, string>
                {
                    { "Component_name", componentName },
                    { "Position", position },
                    { "Size", size },
                    //{ "ComponentType", componentType }
                };

                        //ccommccList.Add(currentObj);
                        if (currentObj.Values.All(v => v != null))
                        {
                            ccommccList.Add(currentObj);
                        }

                        var ocObj = new Dictionary<string, string>
                {
                    { "componentName", ocComponentName },
                    { "startPoint", ocStart },
                    { "endingPoint", ocEnd }
                };

                        if (ocObj.Values.All(v => v != null))
                        {
                            ocList.Add(ocObj);
                        }
                    }

                    return (ccommccList, ocList);
             }
    }
}






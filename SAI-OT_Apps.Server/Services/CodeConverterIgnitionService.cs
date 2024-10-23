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
        public List<(int Index, string Name, string TemplatePath, bool Converted)> ExtractTemplatesFromFile(string filePath)
        {
            var templates = new HashSet<(int Index, string Name, string TemplatePath, bool Converted)>();
            var xmlDoc = new XmlDocument();
            int Index = 0;
            xmlDoc.Load(filePath);  // Load XML from the file

            var templateNodes = xmlDoc.SelectNodes("//c[@cls='com.inductiveautomation.factorypmi.application.components.template.TemplateHolder']");

            foreach (XmlNode templateNode in templateNodes)
            {
                var nameNode = templateNode.SelectSingleNode(".//str");
                var templatePathNode = templateNode.SelectSingleNode(".//c-c[@m='setTemplatePath']/str");

                if (nameNode != null && templatePathNode != null)
                {
                    //Find if the template is alredy converted
                    string pathModified = GetPathExcludingLastTwoItems(filePath);
                    string templatePath = templatePathNode.InnerText.Replace("/", "\\");
                    pathModified = pathModified + "\\Perspective\\Templates\\" + templatePath + ".xml";
                    bool Converted = File.Exists(pathModified);
                    Index++;
                    templates.Add((Index, nameNode.InnerText, templatePathNode.InnerText, Converted));
                }
            }

            List<(int Index, string Name, string TemplatePath, bool Converted)> templateList = new List<(int Index, string Name, string TemplatePath, bool Converted)>(templates);

            return templateList;
        }

        static string GetPathExcludingLastTwoItems(string fullPath)
        {
            // Get the directory info for the provided path
            DirectoryInfo directoryInfo = new DirectoryInfo(fullPath);

            // Traverse up two levels in the directory hierarchy
            DirectoryInfo parentDirectory = directoryInfo.Parent?.Parent?.Parent;

            // Return the full path of the parent directory, or null if it doesn't exist
            return parentDirectory?.FullName;
        }

    }

   


}






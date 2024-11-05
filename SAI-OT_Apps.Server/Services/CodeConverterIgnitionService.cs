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
        public List<(int Index, string Name, string TemplatePath, bool screenExists, bool screenConverted)> ExtractTemplatesFromFile(string filePath)
        {
            var templates = new HashSet<(int Index, string Name, string TemplatePath, bool screenExists, bool screenConverted)>();
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
                    string pathScreenConverted = pathModified + "\\Perspective\\Templates\\" + templatePath + ".json";
                    string pathScreenExist = pathModified + "\\Vision\\Templates\\" + templatePath + ".xml";
                    bool screenConverted = File.Exists(pathScreenConverted);
                    bool screenExists = File.Exists(pathScreenExist);
                    string templateName = GetLastWord(templatePathNode.InnerText);
                    Index++;
                    templates.Add((Index, templateName, templatePathNode.InnerText, screenExists, screenConverted));
                }
            }

            List<(int Index, string Name, string TemplatePath, bool screenExists, bool screenConverted)> templateList = new List<(int Index, string Name, string TemplatePath, bool screenExists, bool screenConverted)>(templates);

            // Sort the list by TemplatePath
            templateList = templateList.OrderBy(t => t.TemplatePath).ToList();

            // Update the Index property to start from 1
            for (int i = 0; i < templateList.Count; i++)
            {
                templateList[i] = (i + 1, templateList[i].Name, templateList[i].TemplatePath, templateList[i].screenExists, templateList[i].screenConverted);
            }

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

        static string GetLastWord(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            string[] parts = input.Split('/');
            return parts[^1]; // Using the ^ operator to get the last element
        }

    }

   


}






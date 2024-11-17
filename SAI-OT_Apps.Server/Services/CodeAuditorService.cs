using Opc.Ua;
using Org.BouncyCastle.Asn1.Ocsp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UglyToad.PdfPig.Content;
using PdfiumViewer; // For PdfiumViewer.PdfDocument
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

// Namespace aliases to resolve ambiguity
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;
using PdfiumPdfDocument = PdfiumViewer.PdfDocument;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpRectangle = SixLabors.ImageSharp.Rectangle;
using System.Runtime.Intrinsics.Arm;
using System.Drawing.Imaging;
using SixLabors.ImageSharp.Formats.Png;
using ExcelDataReader;
using System.Data;
using Microsoft.AspNetCore.DataProtection.KeyManagement;


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
        private readonly string _apiKey;
        public CodeAuditorService(IConfiguration configuration)
        {
            _apiKey = configuration["apiKey"];
        }

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

        /*public string GetRoutine(string PLCfilePath)
        {
            try
            {
                // Lê o conteúdo do arquivo XML no caminho fornecido
                string xmlContent = System.IO.File.ReadAllText(PLCfilePath);

                // Retorna o conteúdo XML como string
                return xmlContent;
            }
            catch (Exception ex)
            {
                // Caso ocorra algum erro ao ler o arquivo, exibe a mensagem de erro
                Console.WriteLine($"Erro ao ler o arquivo XML: {ex.Message}");
                return null;
            }
        }*/

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
            Console.WriteLine(routineCode);

            List<Dictionary<string, string>> rungs = new List<Dictionary<string, string>>();
            string preRungAnalysis = "";

            var apiKey = _apiKey; // TODO: Replace with your API key

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
                throw new Exception("API SAIDescriptionAnalysis failed!");
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
        private readonly string _apiKey;
        private string _outputDirectory;
        public CodeAuditorServiceUDT(IConfiguration configuration)
        {
            _apiKey = configuration["apiKey"];
        }
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
                    if (StandardUDTItemName == "IHM_EQP" || StandardUDTItemName == "IHM_CMD")
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
            var apiKey = _apiKey; // TODO: Replace with your API key

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
        public async Task<List<string>> ExtractRungsFromPdf(string pdfPath, string routine, string outputDirectory)
        {
            if (string.IsNullOrEmpty(pdfPath))
            {
                throw new ArgumentNullException(nameof(pdfPath), "The PDF path cannot be null or empty.");
            }

            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("PDF file not found at the specified path.", pdfPath);
            }

            // Define the left margin for detecting rungs
            double leftMarginThreshold = 15; // 15 points from the left

            // Define fixed offsets
            double fixedTopOffset = 20;      // 20 points above the rung
            double fixedBottomOffset = 500;  // 500 points below the rung

            // DPI for rendering the image
            double dpi = 300.0;
            double scale = dpi / 72.0; // 1 point = 1/72 inch

            _outputDirectory = outputDirectory.Replace("\\", "\\\\"); // Adjust the output directory

            Directory.CreateDirectory(_outputDirectory);

            List<string> imagePaths = new List<string>(); // Store the paths of saved images

            try
            {
                using (var pdf = PdfiumPdfDocument.Load(pdfPath))
                using (var document = PdfPigDocument.Open(pdfPath))
                {
                    int totalPages = document.NumberOfPages;

                    // Dictionary to store pages corresponding to each routine
                    Dictionary<string, List<int>> routinePages = new Dictionary<string, List<int>>();

                    // Loop through the PDF pages to locate routines
                    for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
                    {
                        var page = document.GetPage(pageIndex + 1);
                        var words = page.GetWords().ToList();

                        // Identify header words
                        var headerWords = words
                            .Where(w => w.BoundingBox.Left <= 5)
                            .OrderByDescending(w => w.BoundingBox.Top)
                            .ToList();

                        string routineName = headerWords.FirstOrDefault()?.Text.Trim();

                        if (string.IsNullOrEmpty(routineName))
                        {
                            routineName = "UnknownRoutine";
                        }

                        if (!routinePages.ContainsKey(routineName))
                        {
                            routinePages[routineName] = new List<int>();
                        }
                        routinePages[routineName].Add(pageIndex);
                    }

                    // Filter pages of the selected routine
                    string selectedRoutine = routine;
                    List<int> pagesToProcess = routinePages[selectedRoutine];

                    // Loop through the pages of the selected routine
                    foreach (var pageIndex in pagesToProcess)
                    {
                        var page = document.GetPage(pageIndex + 1);
                        var words = page.GetWords().ToList();

                        // Filter all rungs in the left margin
                        var allRungs = words
                            .Where(w =>
                                (int.TryParse(w.Text.Trim(), out _) ||
                                 w.Text.Trim().Equals("(End)", StringComparison.OrdinalIgnoreCase)) &&
                                w.BoundingBox.Left <= leftMarginThreshold)
                            .OrderByDescending(w => w.BoundingBox.Top)
                            .ToList();

                        // If no rungs are found on the page, continue to the next page
                        if (allRungs.Count == 0)
                        {
                            continue;
                        }

                        // Render the page as an image
                        ImageSharpImage pageImage = RenderPageAsImage(pdf, pageIndex, dpi);

                        if (pageImage == null)
                        {
                            continue;
                        }

                        // Process each rung found
                        foreach (var currentRung in allRungs)
                        {
                            double currentY0 = currentRung.BoundingBox.Top;
                            int currentIndex = allRungs.IndexOf(currentRung);

                            if (currentIndex == -1)
                            {
                                continue;
                            }

                            double cropTopPdf = currentY0 + fixedTopOffset;
                            double cropBottomPdf;

                            // Check if there is a next rung on the page
                            if (currentIndex + 1 < allRungs.Count)
                            {
                                var nextRung = allRungs[currentIndex + 1];
                                double nextY0 = nextRung.BoundingBox.Top;

                                double desiredCropBottomPdf = currentY0 - fixedBottomOffset;
                                double nextRungCropBottomPdf = nextY0 + fixedTopOffset;

                                cropBottomPdf = Math.Max(desiredCropBottomPdf, nextRungCropBottomPdf);
                            }
                            else
                            {
                                double lowestY0 = FindLowestYCoordinate(page);
                                cropBottomPdf = Math.Max(lowestY0 - 20, 10);
                            }

                            cropTopPdf = Math.Min(cropTopPdf, page.Height);
                            cropBottomPdf = Math.Max(cropBottomPdf, 10);

                            double image_y0 = (page.Height - cropTopPdf) * scale;
                            double image_y1 = (page.Height - cropBottomPdf) * scale;

                            image_y0 = Math.Max(0, Math.Min(pageImage.Height, image_y0));
                            image_y1 = Math.Max(0, Math.Min(pageImage.Height, image_y1));

                            if (image_y0 > image_y1)
                            {
                                double temp = image_y0;
                                image_y0 = image_y1;
                                image_y1 = temp;
                            }

                            int cropX0 = 0;
                            int cropY0 = (int)Math.Floor(image_y0);
                            int cropWidth = pageImage.Width;
                            int cropHeight = (int)Math.Ceiling(image_y1 - image_y0);

                            if (cropY0 + cropHeight > pageImage.Height)
                            {
                                cropHeight = pageImage.Height - cropY0;
                            }

                            // **[Step 2]** Save the cropped image
                            try
                            {
                                var croppedImage = pageImage.Clone(ctx =>
                                    ctx.Crop(new ImageSharpRectangle(cropX0, cropY0, cropWidth, cropHeight)));

                                string outputFilename = $"{routine}_rung_{currentRung.Text}.png";
                                //string fullOutputPath = Path.Combine(_outputDirectory, outputFilename);

                                using (var stream = new FileStream(outputFilename, FileMode.Create)) //was fullOutputPath
                                {
                                    croppedImage.Save(stream, new PngEncoder());
                                }

                                // Add the image path to the list
                                imagePaths.Add(outputFilename); //was fullOutputPath
                            }
                            catch (Exception ex)
                            {
                                // Handle exception
                            }
                        }
                    }
                }

                return imagePaths; // Return the list of image paths
            }
            catch (DllNotFoundException dllEx)
            {
                throw new Exception($"DLL not found: {dllEx.Message}. Make sure 'pdfium.dll' is referenced correctly.");
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred: {ex.Message}");
            }
        }
        private static Image<Rgba32> RenderPageAsImage(PdfiumPdfDocument pdf, int pageIndex, double dpi)
        {
            try
            {
                using (var bitmap = pdf.Render(pageIndex, (int)dpi, (int)dpi, PdfRenderFlags.CorrectFromDpi))
                {
                    // Convert System.Drawing.Bitmap to ImageSharp Image
                    using (var ms = new MemoryStream())
                    {
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Seek(0, SeekOrigin.Begin);
                        var imageSharpImage = ImageSharpImage.Load<Rgba32>(ms);
                        return imageSharpImage;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static double FindLowestYCoordinate(UglyToad.PdfPig.Content.Page page)
        {
            // Define the bottom margin to ignore (10 points)
            double bottomMargin = 10.0;

            // Retrieve all words on the page that are above the bottom margin
            var wordsAboveMargin = page.GetWords()
                                       .Where(w => w.BoundingBox.Bottom >= bottomMargin)
                                       .ToList();

            // Initialize with the bottom margin if no words are found above it
            double lowestY = wordsAboveMargin.Any() ? wordsAboveMargin.Min(w => w.BoundingBox.Bottom) : bottomMargin;
            return lowestY;
        }

        public void DeleteImages(string outputDirectory)
        {
            // Set the current path as standard
            if (string.IsNullOrEmpty(outputDirectory))
            {
                
                outputDirectory = _outputDirectory;
            }

            // Checks if the folder exists
            if (Directory.Exists(outputDirectory))
            {
                // Filter and exclude ontly image files (ex: .png, .jpg, .jpeg)
                var imageFiles = Directory.GetFiles(outputDirectory, "*.*")
                                          .Where(file => file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                         file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                         file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                foreach (var filePath in imageFiles)
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine($"Error in excluiding file {filePath}: {ex.Message}");
                    }
                }
            }
            else
            {
                //Console.WriteLine("Folder not found.");
            }
        }

        public async Task<string> SAICodeAuditorInterlockUDT(string xmlContent)
        {
            var apiKey = _apiKey; // TODO: Replace with your API key
            Console.WriteLine("Dentro do SAICodeAuditorInterlockUDT: " + xmlContent);
            var teste = xmlContent.ToString();
            Console.WriteLine("Dentro do SAICodeAuditorInterlockUDT to string: " + teste);

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/671fd851c05a8b6c9ed1eb71/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["request"] = teste //xmlContent
                    }
                })
                .AddHeader("X-Api-Key", apiKey)
                .AddHeader("Authorization", $"Bearer {_apiKey}")
                .AddHeader("Content-Type", "application/json");
                //.AddParameter("application/xml", xmlContent, ParameterType.RequestBody);

            Console.WriteLine("request: " + request);
            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                try
                {
                    var response = await client.ExecuteAsync(request, cancellationTokenSource.Token);
                    Console.WriteLine("Response Content: " + response.Content);
                    if (response.IsSuccessful)
                    {
                        string SAICodeAuditorInterlockUDTResult = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                        return SAICodeAuditorInterlockUDTResult;
                    }
                    else
                    {
                        throw new Exception("API SAICodeAuditorInterlockUDT failed!");
                    }
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("A requisição foi cancelada devido ao timeout.");
                    return null; // Retorna null ou outro valor adequado
                }
            }

            return null; // Retorna null caso a requisição falhe
        }

        /*public async Task<string> SAICodeAuditorInterlockUDT(string xmlContent)
        {
            var apiKey = _apiKey;
            Console.WriteLine("Dentro do SAICodeAuditorInterlockUDT: " + xmlContent);

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/671fd851c05a8b6c9ed1eb71/execute", Method.Post)
                .AddHeader("X-Api-Key", apiKey)
                .AddHeader("Authorization", $"Bearer {_apiKey}")
                .AddHeader("Content-Type", "application/json")
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["request"] = xmlContent
                    }
                });

            Console.WriteLine("request: " + request);

            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                try
                {
                    var response = await client.ExecuteAsync(request, cancellationTokenSource.Token);
                    Console.WriteLine("Response Content: " + response.Content);
                    Console.WriteLine("Status Code: " + response.StatusCode);
                    Console.WriteLine("Response Headers: " + response.Headers);
                    Console.WriteLine("Error Message: " + response.ErrorMessage);
                    Console.WriteLine("Raw Response: " + response.RawBytes);

                    if (response.IsSuccessful)
                    {
                        return response.Content; // Retorna o conteúdo direto
                    }
                    else
                    {
                        throw new Exception("API SAICodeAuditorInterlockUDT failed!");
                    }
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("A requisição foi cancelada devido ao timeout.");
                    return null;
                }
            }
        }*/




        public string ConvertExcelToCsv(Stream excelStream)
        {
            // Registra o provedor de codificação para suportar o encoding 1252
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // Defina o caminho temporário para salvar o CSV
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

            using (var reader = ExcelReaderFactory.CreateReader(excelStream))
            using (var writer = new StreamWriter(tempFilePath))
            {
                // Lê as planilhas do Excel para um DataSet
                var result = reader.AsDataSet();

                // Assume que queremos apenas a primeira planilha
                var dataTable = result.Tables[0];

                // Escreve os dados da planilha no arquivo CSV
                foreach (DataRow row in dataTable.Rows)
                {
                    var fields = row.ItemArray.Select(field =>
                        field.ToString().Replace(",", ";")); // Substitui vírgulas para evitar conflitos no CSV
                    writer.WriteLine(string.Join(",", fields));
                }
            }

            return tempFilePath;
        }
    }
}

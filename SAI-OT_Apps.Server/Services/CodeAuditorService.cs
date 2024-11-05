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

        public async Task<string> ExtractRungsFromPdf(string pdfPath, string routine, string outputDirectory) //was string
        {
            if (string.IsNullOrEmpty(pdfPath))
            {
                throw new ArgumentNullException(nameof(pdfPath), "O caminho do PDF não pode ser nulo ou vazio.");
            }

            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("Arquivo PDF não encontrado no caminho especificado.", pdfPath);
            }

            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException("Arquivo PDF não encontrado no caminho especificado.", pdfPath);
            }
            // Define the left margin threshold in points
            double leftMarginThreshold = 15; // 15 points from the left

            // Define fixed offsets in points
            double fixedTopOffset = 20;      // 20 points above the rung
            double fixedBottomOffset = 500;  // 500 points below the rung

            // DPI for image rendering
            double dpi = 300.0;
            double scale = dpi / 72.0; // 1 point = 1/72 inch

            //string outputDirectory = Path.Combine(Path.GetTempPath(), "ExtractedRungs");
            _outputDirectory = outputDirectory.Replace("\\", "\\\\");//(Path.DirectorySeparatorChar, '\\');

            Directory.CreateDirectory(_outputDirectory);

            try
            {
                // **[Step 1]** Create a list to store cropped images
                List<ImageSharpImage> croppedImages = new List<ImageSharpImage>();
                using (var pdf = PdfiumPdfDocument.Load(pdfPath))
                using (var document = PdfPigDocument.Open(pdfPath))
                {
                    int totalPages = document.NumberOfPages;

                    bool continueProcessing = true;


                    // Dictionary to hold routine names and their corresponding page indices
                    Dictionary<string, List<int>> routinePages = new Dictionary<string, List<int>>();

                    for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
                    {
                        UglyToad.PdfPig.Content.Page page = document.GetPage(pageIndex + 1);
                        var words = page.GetWords().ToList();

                        // Identify header words (x ~ 0 and highest y)
                        var headerWords = words
                            .Where(w => w.BoundingBox.Left <= 5) // Adjust threshold as needed
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

                    string selectedRoutine = routine;

                    bool anyRungFound = false;

                    List<int> pagesToProcess = routinePages[selectedRoutine];
                    int totalPagesInRoutine = pagesToProcess.Count;

                    for (int i = 0; i < totalPagesInRoutine; i++)
                    {
                        int pageIndex = pagesToProcess[i];
                        UglyToad.PdfPig.Content.Page page = document.GetPage(pageIndex + 1);
                        var words = page.GetWords().ToList(); // Convert to List for indexing

                        // Collect all rung occurrences on this page (only rungs within left margin)
                        var allRungs = words
                            .Where(w =>
                                (int.TryParse(w.Text.Trim(), out _) ||
                                    w.Text.Trim().Equals("(End)", StringComparison.OrdinalIgnoreCase)) &&
                                w.BoundingBox.Left <= leftMarginThreshold)
                            .OrderByDescending(w => w.BoundingBox.Top)
                            .ToList();

                        // Collect specific rung occurrences based on user input
                        var specificRungs = allRungs.ToList();

                        if (specificRungs.Count == 0)
                        {
                            //Console.WriteLine("No rungs found on this page.\n");
                            continue;
                        }

                        // Render the page as image at specified DPI once per page
                        ImageSharpImage pageImage = RenderPageAsImage(pdf, pageIndex, dpi);

                        if (pageImage == null)
                        {
                            //Console.WriteLine($"Failed to render page {pageIndex + 1}\n");
                            continue;
                        }

                        // Iterate through each specific rung occurrence
                        foreach (var currentRung in specificRungs)
                        {
                            anyRungFound = true;

                            double currentY0 = currentRung.BoundingBox.Top;

                            // Find the index of the current rung in the allRungs list
                            int currentIndex = allRungs.IndexOf(currentRung);

                            if (currentIndex == -1)
                            {
                                //Console.WriteLine("Error: Current rung not found in allRungs list. Skipping this rung.\n");
                                continue;
                            }

                            double cropTopPdf = currentY0 + fixedTopOffset;
                            double cropBottomPdf;
                            string nextRungInfo = "N/A";

                            if (currentIndex + 1 < allRungs.Count)
                            {
                                // There is a next rung on the same page
                                var nextRung = allRungs[currentIndex + 1];
                                double nextY0 = nextRung.BoundingBox.Top;

                                // Calculate cropBottomPdf as the maximum of:
                                // 1. fixedBottomOffset below the current rung
                                // 2. fixedTopOffset above the next rung
                                double desiredCropBottomPdf = currentY0 - fixedBottomOffset;
                                double nextRungCropBottomPdf = nextY0 + fixedTopOffset;

                                // Ensure that we don't crop beyond the next rung
                                cropBottomPdf = Math.Max(desiredCropBottomPdf, nextRungCropBottomPdf);

                                nextRungInfo = $"Next rung '{nextRung.Text}' at y0: {nextY0}";
                            }
                            else
                            {
                                // No next rung found, use dynamic end offset
                                double lowestY0 = FindLowestYCoordinate(page);

                                // Subtract an additional 20 points from the lowestY0 for cropping (as per your deliberate change)
                                double newCropBottomPdf = lowestY0 - 20;

                                // Ensure that cropBottomPdf does not go below 10 points
                                cropBottomPdf = Math.Max(newCropBottomPdf, 10);

                                nextRungInfo = "No next rung found, using dynamic end offset based on the lowest character above the bottom margin and subtracting 20 points.";
                            }

                            // Clamp y-coordinates to page boundaries
                            cropTopPdf = Math.Min(cropTopPdf, page.Height);
                            cropBottomPdf = Math.Max(cropBottomPdf, 10);

                            // Convert PDF coordinates to image pixels
                            // PDF origin is bottom-left; Image origin is top-left
                            double image_y0 = (page.Height - cropTopPdf) * scale; // Upper Y in image
                            double image_y1 = (page.Height - cropBottomPdf) * scale; // Lower Y in image

                            // Clamp image_y0 and image_y1 to image bounds
                            image_y0 = Math.Max(0, Math.Min(pageImage.Height, image_y0));
                            image_y1 = Math.Max(0, Math.Min(pageImage.Height, image_y1));

                            // Ensure image_y0 < image_y1 for ImageSharp cropping
                            if (image_y0 > image_y1)
                            {
                                // Swap to maintain y0 < y1
                                double temp = image_y0;
                                image_y0 = image_y1;
                                image_y1 = temp;
                            }

                            // Calculate crop rectangle in pixels
                            int cropX0 = 0;
                            int cropY0 = (int)Math.Floor(image_y0);
                            int cropWidth = pageImage.Width; // Full width
                            int cropHeight = (int)Math.Ceiling(image_y1 - image_y0);

                            // Ensure crop dimensions are within image bounds
                            if (cropY0 + cropHeight > pageImage.Height)
                            {
                                //Console.WriteLine($"Adjusting crop height from {cropHeight} to fit within image bounds.");
                                cropHeight = pageImage.Height - cropY0;

                                if (cropHeight <= 0)
                                {
                                    //Console.WriteLine($"Adjusted crop height is invalid. Skipping cropping.\n");
                                    continue;
                                }
                            }

                            // **[Step 2]** Assign the cropped image to the list instead of saving
                            try
                            {
                                var croppedImage = pageImage.Clone(ctx =>
                                    ctx.Crop(new ImageSharpRectangle(cropX0, cropY0, cropWidth, cropHeight)));

                                // Generate timestamp
                                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                                // Define the output filename with timestamp
                                string outputFilename = Path.Combine(_outputDirectory, $"{routine}_rung_{currentRung.Text}.png");//_page_{pageIndex + 1}.png");

                                // Salva a imagem recortada, sobrescrevendo se necessário
                                using (var stream = new FileStream(outputFilename, FileMode.Create))
                                {
                                    // Salva a imagem no arquivo
                                    croppedImage.Save(stream, new PngEncoder());
                                }

                                // **[Step 3]** Add the cropped image to the list
                                croppedImages.Add(croppedImage);
                            }
                            catch (Exception ex)
                            {
                                //Console.WriteLine($"Failed to process cropped image: {ex.Message}\n");
                            }
                        }
                    }
                }

                return _outputDirectory;
            }
            catch (DllNotFoundException dllEx)
            {
                throw new Exception($"DLL not found: {dllEx.Message}. Ensure 'pdfium.dll' is correctly referenced in your project.");
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

        public void DeleteOutputDirectory(string outputDirectory)
        {
            // Se o caminho não for fornecido, utiliza o _outputDirectory armazenado
            if (string.IsNullOrEmpty(outputDirectory))
            {
                Console.WriteLine(_outputDirectory); // Deve imprimir o caminho com barras invertidas

                outputDirectory = _outputDirectory;
            }

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true); // Exclui o diretório e seus arquivos
            }
        }
    }
}

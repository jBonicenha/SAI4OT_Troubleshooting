using ExcelDataReader;
using RestSharp;
using System.Data;
using System.Text.RegularExpressions;

namespace SAI_OT_Apps.Server.Services
{
    public class CodeGeneratorService
    {
        private readonly string _apiKey;

        public CodeGeneratorService(IConfiguration configuration)
        {
            _apiKey = configuration["apiKey"];
        }
        public async Task<string> generatePLCRoutineCode(string routineName, string userRequest)
        {
            var apiKey = _apiKey; // TODO: Replace with your API key

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/669ac3797b02a144a0f7bfa4/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["routineName"] = routineName,
                        ["request"] = userRequest,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                return result;
                //var resultFormated = ExtractXml(result);
                //string filePath = "C:\\SAI\\" + routineName + ".L5X"; // Path to the output file
                //await File.WriteAllTextAsync(filePath, resultFormated);
                //return "File generated successfully";
            }
            else
            {
                return "Failed to generate PLC routine code";
            }

        }

        public async Task<string> downloadPLCRoutineCode(string routineName, string PLCRoutineCode)
        {
            try
            {
                var resultFormated = ExtractXml(PLCRoutineCode);
                string filePath = "C:\\SAI\\SAICodeGenerator\\" + routineName + ".L5X"; // Path to the output file
                await File.WriteAllTextAsync(filePath, resultFormated);
                return "File generated successfully";
            }
            catch
            {
                return "Failed to generate PLC routine code";
            }
        }

        static string RemoveFormatIndications(string input)
        {
            // Find the start of the XML content
            int startIndex = input.IndexOf("<?xml");
            int endIndex = input.IndexOf("</Controller>\r\n</RSLogix5000Content>");

            // If the starting point is found and the endpoint is also found
            if (startIndex != -1 && endIndex != -1)
            {
                // Extract the relevant portion including the end tag
                return input.Substring(startIndex, endIndex + "</Controller>\r\n</RSLogix5000Content>".Length - startIndex).Trim();
            }
            else
            {
                // If either tag is not found, return an empty string or the original input (based on your requirement)
                return string.Empty; // or return input; if you want to return the original string
            }
        }

        static string ExtractXml(string input)
        {
            // Regular expression to match XML content
            string pattern = @"(<([^>]+)>)";
            MatchCollection matches = Regex.Matches(input, pattern);

            // If there are no matches, return an empty string
            if (matches.Count == 0)
            {
                return string.Empty;
            }

            // Build the XML string from matches
            string xmlContent = string.Join("", matches);
            return xmlContent;
        }

        public async Task<string> SAICodeGeneratorXML(string csvContent)
        {
            var apiKey = _apiKey; // TODO: Replace with your API key

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/670679dd85214268a4d1dcbf/execute", Method.Post)
                .AddBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["request"] = csvContent,
                    }
                })
                .AddHeader("X-Api-Key", apiKey);
            var response = await client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                string SAICodeAuditorInterlockUDTResult = System.Text.Json.JsonSerializer.Deserialize<string>(response.Content);
                return SAICodeAuditorInterlockUDTResult;
            }
            else
            {
                throw new Exception("API SAICodeGeneratorXML failed!");
            }
        }

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

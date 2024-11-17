using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;
using static SAI_OT_Apps.Server.Controllers.CodeAuditorController;

namespace SAI_OT_Apps.Server.Controllers
{
    public class CodeAuditorController : Controller
    {
        private CodeAuditorService _codeAuditorService;

        public CodeAuditorController(CodeAuditorService codeAuditorService)
        {
            _codeAuditorService = codeAuditorService;
        }

        public class RoutineCodeRequest
        {
            public string RoutineCode { get; set; }
        }

        [HttpPost("RoutinesList")]
        public async Task<IActionResult> RoutinesList([FromQuery] string PLCfilePath)
        {
            List<string> result = new List<string>();
            try
            {
                result = _codeAuditorService.RoutinesList(PLCfilePath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("GetRoutineByName")]
        public async Task<IActionResult> GetRoutineByName([FromQuery] string PLCfilePath, string routineName)
        {
            string result;
            try
            {
                result = _codeAuditorService.GetRoutineByName(PLCfilePath, routineName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("GetRoutine")]
        public async Task<IActionResult> GetRoutine([FromQuery] string PLCfilePath)
        {
            if (string.IsNullOrEmpty(PLCfilePath))
            {
                return BadRequest("PLC file path cannot be empty.");
            }

            try
            {
                // Lê o conteúdo do arquivo XML a partir do caminho fornecido
                string xmlContent = System.IO.File.ReadAllText(PLCfilePath);

                // Retorna o conteúdo XML como string
                return Ok(xmlContent);
            }
            catch (Exception ex)
            {
                // Retorna erro se não conseguir ler o arquivo
                Console.WriteLine($"Erro ao ler o arquivo XML: {ex.Message}");
                return StatusCode(500, "Erro ao ler o arquivo XML.");
            }
        }

        [HttpPost("UpdateRoutineWithComments")]
        public async Task<IActionResult> UpdateRoutineWithComments([FromQuery] string PLCfilePath, [FromQuery] string routineName, [FromBody] List<RungDescription> RoutineDescriptionRevised)
        {
            string result;
            try
            {
                result = _codeAuditorService.UpdateRoutineWithComments(PLCfilePath, routineName, RoutineDescriptionRevised);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("SAIDescriptionAnalysis")]
        public async Task<IActionResult> SAIDescriptionAnalysis([FromBody] RoutineCodeRequest request)
        {
            try
            {
                var (preRungAnalysis, rungs) = await _codeAuditorService.SAIDescriptionAnalysis(request.RoutineCode);
                var result = new
                {
                    PreRungAnalysis = preRungAnalysis,
                    RungAnalysis = rungs
                };
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
    }

    public class CodeAuditorControllerUDT : Controller
    {
        public class RungExtractionRequest
        {
            public string PdfPath { get; set; }
            public string Routine { get; set; }
        }

        private CodeAuditorServiceUDT _codeAuditorServiceUDT;

        public CodeAuditorControllerUDT(CodeAuditorServiceUDT codeAuditorServiceUDT)
        {
            _codeAuditorServiceUDT = codeAuditorServiceUDT;
        }

        [HttpPost("AuditUDTAnalysis")]
        public async Task<IActionResult> AuditUDTCode([FromQuery] string PLCfilePath)
        {
            try
            {
                var result = _codeAuditorServiceUDT.AuditUDTCode(PLCfilePath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpGet("extract-rungs")]
        public async Task<IActionResult> ExtractRungsFromPdf(string plcFilePath, string routineName)
        {
            string outputDirectory = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(outputDirectory);

            // Call the Service to process the extraction, passing the outputDirectory
            var imagePaths = await _codeAuditorServiceUDT.ExtractRungsFromPdf(plcFilePath, routineName, outputDirectory);

            // Create URLs for the images that are accessible via HTTP
            var imageUrls = imagePaths.Select(fileName => Url.Action("GetImage", new { fileName })).ToList();

            return Ok(new { imageUrls });
        }

        [HttpGet("ListImages")]
        public IActionResult ListImages()
        {
            string directoryPath = Path.Combine(Path.GetTempPath(), "ExtractedRungs");
            if (!Directory.Exists(directoryPath))
            {
                return NotFound("Directory not found");
            }

            // List all image files in the specified directory
            var imageFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => file.EndsWith(".png") || file.EndsWith(".jpg") || file.EndsWith(".jpeg"))
                .Select(file => Path.GetFileName(file))
                .ToList();

            return Ok(imageFiles);
        }

        [HttpGet("GetImage")]
        public IActionResult GetImage(string fileName)
        {
            // Get the current project directory
            string projectDirectory = Directory.GetCurrentDirectory();

            //string filePath = Path.Combine(Path.GetTempPath(), "ExtractedRungs", fileName);
            string filePath = Path.Combine(projectDirectory, fileName);
            //Console.WriteLine("GetImage filepath: " + filePath);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Image not found.");
            }

            byte[] imageBytes = System.IO.File.ReadAllBytes(filePath);
            return File(imageBytes, "image/png");  // Adjust the content type according to the image format
        }

        [HttpDelete("delete-images")]
        public IActionResult DeleteImages()
        {
            try
            {
                // Get the current project directory
                string projectDirectory = Directory.GetCurrentDirectory();

                // Call the function to delete the images without deleting the directory
                _codeAuditorServiceUDT.DeleteImages(projectDirectory);
                return Ok("Images deleted successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting the images: {ex.Message}");
            }
        }

        /*[HttpPost("upload-excel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // Chama a service para converter a planilha
            string csvFilePath;
            using (var stream = file.OpenReadStream())
            {
                csvFilePath = _codeAuditorServiceUDT.ConvertExcelToCsv(stream);
            }

            // Lê o arquivo CSV e o retorna como um download
            var fileBytes = await System.IO.File.ReadAllBytesAsync(csvFilePath);

            // Remove o arquivo temporário após o download
            System.IO.File.Delete(csvFilePath);

            var csvResult = File(fileBytes, "text/csv", $"{Path.GetFileNameWithoutExtension(file.FileName)}.csv");

            var saiCodeAuditorInterlockResult = await _codeAuditorServiceUDT.SAICodeAuditorInterlockUDT(csvResult);
        }*/

        /*[HttpPost("upload-excel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // Chama a service para converter a planilha e obter o caminho do CSV
            string csvFilePath;
            using (var stream = file.OpenReadStream())
            {
                csvFilePath = _codeAuditorServiceUDT.ConvertExcelToCsv(stream);
            }

            // Lê o conteúdo do arquivo CSV como uma string
            string csvContent = await System.IO.File.ReadAllTextAsync(csvFilePath);

            // Remove o arquivo temporário após a leitura
            System.IO.File.Delete(csvFilePath);
            Console.WriteLine(csvContent);

            var saiCodeInterlockResult = await _codeAuditorServiceUDT.SAICodeAuditorInterlockUDT(csvContent);

            // Retorna o conteúdo do CSV como JSON
            return Ok(saiCodeInterlockResult);
        }*/

        [HttpPost("send-interlock")]
        public async Task<IActionResult> SendInterlock([FromBody] string routineCode)
        {
            if (string.IsNullOrEmpty(routineCode))
            {
                return BadRequest("Routine code cannot be empty.");
            }

            try
            {
                // Verificar se routineCode não está vazio ou nulo
                Console.WriteLine($"Received routine code: {routineCode}");

                // Chama o serviço para processar o código interlock
                var saiCodeInterlockResult = await _codeAuditorServiceUDT.SAICodeAuditorInterlockUDT(routineCode);

                // Verifique se o resultado está vazio
                Console.WriteLine($"Result from service: {saiCodeInterlockResult}");

                return Ok(saiCodeInterlockResult);
            }
            catch (Exception ex)
            {
                // Capture e logue o erro
                Console.WriteLine($"Error occurred: {ex.Message}");
                return StatusCode(500, "Erro ao processar o código interlock.");
            }
        }

    }
}

using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;

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

        //[HttpPost("extract-rungs")]
        /*public IActionResult ExtractRungs([FromBody] RungExtractionRequest request) //async Task<IActionResult> ExtractRungs([FromBody] RungExtractionRequest request)
        {
            // Chama a extração e obtém o caminho do diretório
            string outputDirectory = _codeAuditorServiceUDT.ExtractRungsFromPdf(request.PdfPath, request.Routine);
            return Ok(outputDirectory); // Retorna o caminho como resposta, se necessário
        }*/
        [HttpGet("extract-rungs")]
        public async Task<IActionResult> ExtractRungsFromPdf(string plcFilePath, string routineName)
        {
            Console.WriteLine($"Recebido plcFilePath: {plcFilePath}");
            Console.WriteLine($"Recebido routineName: {routineName}");

            // Gere um ID único para identificar a pasta temporária de cada solicitação
            //string uniqueId = Guid.NewGuid().ToString();
            string outputDirectory = Path.Combine(Path.GetTempPath(), "ExtractedRungs");//, uniqueId);
            Directory.CreateDirectory(outputDirectory);

            // Chame a Service para processar a extração, passando o outputDirectory
            await _codeAuditorServiceUDT.ExtractRungsFromPdf(plcFilePath, routineName, outputDirectory);

            // Retorne o caminho para a pasta temporária na resposta JSON
            //string temporaryPathUrl = $"/temp-images/";//{uniqueId}";
            //return Ok(new { temporaryPath = temporaryPathUrl });
            return Ok( new { outputDirectory });
        }

        [HttpDelete("delete-output-directory")]
        public IActionResult DeleteOutputDirectory([FromQuery] string outputDirectory)
        {
            try
            {
                _codeAuditorServiceUDT.DeleteOutputDirectory(null); // Chama sem parâmetro para usar o _outputDirectory
                return Ok("Diretório e arquivos excluídos com sucesso.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao excluir o diretório: {ex.Message}");
            }
        }

        [HttpGet("get-image/{fileName}")]
        public IActionResult GetImage(string fileName)
        {
            string imagePath = Path.Combine(Path.GetTempPath(), "ExtractedRungs", fileName);

            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound();
            }

            var image = System.IO.File.OpenRead(imagePath);
            return File(image, "image/png"); // Altere para o tipo correto se necessário
        }

        [HttpGet("ListImages")]
        public IActionResult ListImages(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return NotFound("Directory not found");
            }

            // Lista todos os arquivos de imagem no diretório especificado
            var imageFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => file.EndsWith(".png") || file.EndsWith(".jpg") || file.EndsWith(".jpeg"))
                .Select(file => Path.GetFileName(file))
                .ToList();

            return Ok(imageFiles);
        }
    }

}

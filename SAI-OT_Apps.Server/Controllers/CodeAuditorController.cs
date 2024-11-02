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

        [HttpPost("extract-rungs")]
        public IActionResult ExtractRungs([FromBody] RungExtractionRequest request) //async Task<IActionResult> ExtractRungs([FromBody] RungExtractionRequest request)
        {
            /*if (string.IsNullOrWhiteSpace(request.PdfPath) || string.IsNullOrWhiteSpace(request.Routine))
            {
                return BadRequest("Both 'pdfPath' and 'routine' parameters are required.");
            }

            if (!System.IO.File.Exists(request.PdfPath))
            {
                return NotFound("The specified PDF file does not exist.");
            }

            try
            {
                _codeAuditorServiceUDT.ExtractRungsFromPdf(request.PdfPath, request.Routine);
                return Ok("Rungs extracted successfully.");
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }*/

            // Chama a extração e obtém o caminho do diretório
            string outputDirectory = _codeAuditorServiceUDT.ExtractRungsFromPdf(request.PdfPath, request.Routine);
            return Ok(outputDirectory); // Retorna o caminho como resposta, se necessário
        }

        [HttpDelete("delete-output-directory")]
        public IActionResult DeleteOutputDirectory([FromQuery] string outputDirectory)
        {
            /*if (string.IsNullOrEmpty(outputDirectory))
            {
                return BadRequest("O caminho do diretório não pode ser vazio.");
            }

            try
            {
                _codeAuditorServiceUDT.DeleteOutputDirectory(outputDirectory);
                return Ok("Diretório e arquivos excluídos com sucesso.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao excluir o diretório: {ex.Message}");
            }*/
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
    }

}

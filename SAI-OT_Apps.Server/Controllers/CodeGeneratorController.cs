using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;

namespace SAI_OT_Apps.Server.Controllers
{
    public class CodeGeneratorController : Controller
    {
        private CodeGeneratorService _codeGeneratorService;
        public CodeGeneratorController(CodeGeneratorService codeAuditorServiceUDT)
        {
            _codeGeneratorService = codeAuditorServiceUDT;
        }

        [HttpPost("generatePLCRoutineCode")]
        public async Task<IActionResult> generatePLCRoutineCode([FromQuery] string routineName, string userRequest)
        {
            try
            {
                var result = await _codeGeneratorService.generatePLCRoutineCode(routineName, userRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("downloadPLCRoutineCode")]
        public async Task<IActionResult> downloadPLCRoutineCode([FromQuery] string routineName, [FromBody] string PLCRoutineCode)
        {
            try
            {
                var result = await _codeGeneratorService.downloadPLCRoutineCode(routineName, PLCRoutineCode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("upload-generator-excel")]
        public async Task<IActionResult> UploadExcel([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // Chama a service para converter a planilha e obter o caminho do CSV
            string csvFilePath;
            using (var stream = file.OpenReadStream())
            {
                csvFilePath = _codeGeneratorService.ConvertExcelToCsv(stream);
            }

            // Lê o conteúdo do arquivo CSV como uma string
            string csvContent = await System.IO.File.ReadAllTextAsync(csvFilePath);

            // Remove o arquivo temporário após a leitura
            System.IO.File.Delete(csvFilePath);
            //Console.WriteLine(csvContent);

            var saiCodeInterlockResult = await _codeGeneratorService.SAICodeGeneratorXML(csvContent);

            // Retorna o conteúdo do CSV como JSON
            return Ok(saiCodeInterlockResult);
        }
    }
}

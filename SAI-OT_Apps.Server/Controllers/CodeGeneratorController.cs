using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;

namespace SAI_OT_Apps.Server.Controllers
{
    public class CodeGeneratorController : Controller
    {
        private CodeGeneratorService _codeGeneratorService;
        public CodeGeneratorController()
        {
            _codeGeneratorService = new CodeGeneratorService(); // Instantiate the service here
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
    }
}

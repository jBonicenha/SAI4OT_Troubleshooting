using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;

namespace SAI_OT_Apps.Server.Controllers
{
    public class CodeConverterIgnitionController : Controller
    {
        private CodeConverterIgnitionService _codeConverterIgnitionService;

        public CodeConverterIgnitionController()
        {
            _codeConverterIgnitionService = new CodeConverterIgnitionService(); // Instantiate the service here
        }

        [HttpPost("CodeConverterIgnitionTemplateList")]
        public async Task<IActionResult> CodeConverterIgnitionTemplateList([FromQuery] string projectPath)
        {
            List<string> result = new List<string>();
            try
            {
                result = await _codeConverterIgnitionService.CodeConverterIgnitionTemplateList(projectPath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("CodeConverterIgnitionGenerateScreen")]
        public async Task<IActionResult> CodeConverterIgnitionGenerateScreen([FromQuery] string projectPath)
        {
            try
            {                
                return Ok(_codeConverterIgnitionService.CodeConverterIgnitionGenerateScreen(projectPath));
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

using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;


namespace SAI_OT_Apps.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CodeTesterController : ControllerBase
    {
        private CodeTesterService _codeTesterService;

        public CodeTesterController(CodeTesterService codeTesterService)
        {
            _codeTesterService = codeTesterService;
        }

        [HttpPost("validate-and-generate")]
        public async Task<IActionResult> ValidateAndGenerate([FromForm] IFormFile planilha)
        {
            try
            {
                var result = await CodeTesterService.ValidateAndGenerateJsonFromExcel(planilha);
                return Ok(result); // Retorna o JSON gerado
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro interno: " + ex.Message });
            }
        }


    }
}

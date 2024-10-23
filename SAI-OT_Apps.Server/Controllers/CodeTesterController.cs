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
                var jsonResult = await _codeTesterService.ValidateAndGenerateJsonFromExcel(planilha);

                // Converte a lista de CodeTest para uma string JSON
                var jsonResultString = System.Text.Json.JsonSerializer.Serialize(jsonResult);

                // Log do JSON gerado
                Console.WriteLine("Generated JSON from spreadsheet: " + jsonResultString);

                var saiCodeTesterResult = await _codeTesterService.SAICodeTester(jsonResultString);

                // Log do resultado da chamada API
                Console.WriteLine("SAICodeTester API result: " + saiCodeTesterResult);

                return Ok(saiCodeTesterResult); // Retorna o JSON gerado
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro interno: " + ex.Message });
            }
        }



    }
}

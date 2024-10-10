using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;
using System.IO;

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

        [HttpPost("processar")]
        public IActionResult ProcessarPlanilha([FromForm] IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
            {
                return BadRequest(new { error = "Arquivo não fornecido." });
            }

            try
            {
                var result = _codeTesterService.ProcessarPlanilha(arquivo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("OPCClient")]
        public async Task<IActionResult> OPCClient([FromQuery] List<string> tagList)
        {
            string result = "";
            try
            {
                result = await CodeTesterService.OPCClient(tagList);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("OPCWriteClient")]
        public async Task<IActionResult> OPCWriteClient([FromBody] Dictionary<string, string> tagValues)
        {
            try
            {
                var result = await CodeTesterService.OPCWriteClient(tagValues);
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }

        [HttpPost("UpdateTagResult")]
        public async Task<IActionResult> UpdateTagResult([FromBody] Dictionary<string, string> tagValues, [FromQuery] string tagResult)
        {
            try
            {
                var result = await CodeTesterService.UpdateTagResult(tagValues, tagResult);
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }


    }
}

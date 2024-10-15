using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Models;
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

        [HttpPost("OPCReadAndCheckTags")]
        public async Task<IActionResult> OPCReadAndCheckTags([FromBody] Dictionary<string, List<string>> tagsGroupedByResult)
        {
            try
            {
                var result = await CodeTesterService.OPCReadAndCheckTagsAsync(tagsGroupedByResult);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }

        // Rota para testar as TAGs associadas a um TAG_RESULT
        [HttpGet("TestTagResult")]
        public async Task<IActionResult> TestTagResult([FromQuery] string tagResult)
        {
            try
            {
                CodeTest result = await CodeTesterService.TestTagResult(tagResult);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("TestAllTagResults")]
        public async Task<IActionResult> TestAllTagResults()
        {
            try
            {
                var result = await CodeTesterService.GetAllTagResults();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }

        [HttpPost("ProcessExcel")]
        public async Task<IActionResult> ProcessExcel([FromForm] IFormFile arquivo)
        {
            try
            {
                var result = await CodeTesterService.ProcessExcelFile(arquivo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }

        [HttpPost("PopulateExcel")]
        public async Task<IActionResult> PopulateExcel([FromQuery] string filePath)
        {
            try
            {
                var result = await CodeTesterService.PopulateExcelFromOPC(filePath);
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }

        [HttpPost("GenerateExcelFromOPC")]
        public async Task<IActionResult> GenerateExcelFromOPC()
        {
            try
            {
                var result = await CodeTesterService.GetAllTagsFromOPCAndWriteToExcel();
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }


    }
}

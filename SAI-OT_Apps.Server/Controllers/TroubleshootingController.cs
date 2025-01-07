using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using SAI_OT_Apps.Server.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SAI_OT_Apps.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TroubleshootingController : Controller
    {
        private TroubleshootingService _troubleshootingService;
        public TroubleshootingController(TroubleshootingService troubleshootingService)
        {
            _troubleshootingService = troubleshootingService; // Instantiate the service here
        }

        [HttpPost("TroubleshootingProgram")]
        public async Task<IActionResult> TroubleshootingProgram([FromQuery] string OTETag)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            try
            {
                result = await _troubleshootingService.TroubleshootingProgram(OTETag);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("SAITroubleshootingChatRequest")]
        public async Task<IActionResult> SAITroubleshootingChatRequest([FromQuery] string chatRequest)
        {
            string result = "";
            try
            {
                result = await _troubleshootingService.SAITroubleshootingChatRequest(chatRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("SAITroubleshootingChatResult")]
        public async Task<IActionResult> SAITroubleshootingChatResult([FromQuery] string ogTag, string allWrongTags, string msgInput)
        {
            string result = "";
            try
            {
                result = await _troubleshootingService.SAITroubleshootingChatResult(ogTag, allWrongTags, msgInput);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("SAITroubleshootingCodeExplainer")]
        public async Task<IActionResult> SAITroubleshootingCodeExplainer([FromQuery] string tagName, string msgInput)
        {
            string result = "";
            try
            {
                result = await _troubleshootingService.SAITroubleshootingCodeExplainer(tagName, msgInput);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("SAITroubleshootingConsolidatedResult")]
        public async Task<IActionResult> SAITroubleshootingConsolidatedResult([FromQuery] string majortags, string quertag, string text)
        {
            string result = "";
            try
            {
                result = await _troubleshootingService.SAITroubleshootingConsolidatedResult(majortags, quertag, text);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }


        [HttpPost("SAITroubleshootingMenu")]
        public async Task<IActionResult> SAITroubleshootingMenu([FromQuery] string msgInput)
        {
             try
            {
                var (result1, result2) = await _troubleshootingService.SAITroubleshootingMenu(msgInput);
                return Ok(new { result1, result2 });
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("OPCClient")]
        public async Task<IActionResult> OPCClient([FromQuery] List<string> tagList)
        {
            string result = "";
            try
            {
                result = await TroubleshootingService.OPCClient(tagList);
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



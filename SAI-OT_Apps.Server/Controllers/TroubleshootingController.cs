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
            List<string> result = new List<string>();
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
        public async Task<IActionResult> SAITroubleshootingChatResult([FromQuery] string ogTag, string allWrongTags)
        {
            string result = "";
            try
            {
                result = await _troubleshootingService.SAITroubleshootingChatResult(ogTag, allWrongTags);
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
        public async Task<IActionResult> SAITroubleshootingCodeExplainer([FromQuery] string tagName)
        {
            string result = "";
            try
            {
                result = await _troubleshootingService.SAITroubleshootingCodeExplainer(tagName);
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
            string result = "";
            try
            {
                result = await _troubleshootingService.SAITroubleshootingMenu(msgInput);
                return Ok(result);
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



using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;
using System;

namespace SAI_OT_Apps.Server.Controllers
{
    public class CodeConverterIgnitionController : Controller
    {


        private CodeConverterIgnitionService _codeConverterIgnitionService;

        public CodeConverterIgnitionController()
        {
            _codeConverterIgnitionService = new CodeConverterIgnitionService(); // Instantiate the service here
        }

        public class TemplateDto
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string TemplatePath { get; set; }
            public bool screenExists { get; set; }
            public bool screenConverted { get; set; }

        }

        [HttpPost("ExtractTemplatesFromFile")]
        public async Task<IActionResult> ExtractTemplatesFromFile([FromQuery] string projectPath)
        {
            try
            {
                var result = await Task.Run(() => _codeConverterIgnitionService.ExtractTemplatesFromFile(projectPath));
                var templateDtos = result.Select(t => new TemplateDto { Index = t.Index, Name = t.Name, TemplatePath = t.TemplatePath, screenExists = t.screenExists, screenConverted = t.screenConverted }).ToList();
                return Ok(templateDtos);
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

using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;

namespace SAI_OT_Apps.Server.Controllers
{
    public class CodeAuditorController : Controller
    {
        private CodeAuditorService _codeAuditorService;

        public CodeAuditorController(CodeAuditorService codeAuditorService)
        {
            _codeAuditorService = codeAuditorService;
        }

        public class RoutineCodeRequest
        {
            public string RoutineCode { get; set; }
        }

        [HttpPost("RoutinesList")]
        public async Task<IActionResult> RoutinesList([FromQuery] string PLCfilePath)
        {
            List<string> result = new List<string>();
            try
            {
                result = _codeAuditorService.RoutinesList(PLCfilePath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("GetRoutineByName")]
        public async Task<IActionResult> GetRoutineByName([FromQuery] string PLCfilePath, string routineName)
        {
            string result;
            try
            {
                result = _codeAuditorService.GetRoutineByName(PLCfilePath, routineName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("UpdateRoutineWithComments")]
        public async Task<IActionResult> UpdateRoutineWithComments([FromQuery] string PLCfilePath, [FromQuery] string routineName, [FromBody] List<RungDescription> RoutineDescriptionRevised)
        {
            string result;
            try
            {
                result = _codeAuditorService.UpdateRoutineWithComments(PLCfilePath, routineName, RoutineDescriptionRevised);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("SAIDescriptionAnalysis")]
        public async Task<IActionResult> SAIDescriptionAnalysis([FromBody] RoutineCodeRequest request)
        {
            try
            {
                var (preRungAnalysis, rungs) = await _codeAuditorService.SAIDescriptionAnalysis(request.RoutineCode);
                var result = new
                {
                    PreRungAnalysis = preRungAnalysis,
                    RungAnalysis = rungs
                };
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

    public class CodeAuditorControllerUDT : Controller
    {
        public class RungExtractionRequest
        {
            public string PdfPath { get; set; }
            public string Routine { get; set; }
        }

        private CodeAuditorServiceUDT _codeAuditorServiceUDT;

        public CodeAuditorControllerUDT(CodeAuditorServiceUDT codeAuditorServiceUDT)
        {
            _codeAuditorServiceUDT = codeAuditorServiceUDT;
        }

        [HttpPost("AuditUDTAnalysis")]
        public async Task<IActionResult> AuditUDTCode([FromQuery] string PLCfilePath)
        {
            try
            {
                var result = _codeAuditorServiceUDT.AuditUDTCode(PLCfilePath);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpGet("extract-rungs")]
        public async Task<IActionResult> ExtractRungsFromPdf(string plcFilePath, string routineName)
        {
            string outputDirectory = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(outputDirectory);

            // Call the Service to process the extraction, passing the outputDirectory
            var imagePaths = await _codeAuditorServiceUDT.ExtractRungsFromPdf(plcFilePath, routineName, outputDirectory);

            // Create URLs for the images that are accessible via HTTP
            var imageUrls = imagePaths.Select(fileName => Url.Action("GetImage", new { fileName })).ToList();

            return Ok(new { imageUrls });
        }

        [HttpGet("ListImages")]
        public IActionResult ListImages()
        {
            string directoryPath = Path.Combine(Path.GetTempPath(), "ExtractedRungs");
            if (!Directory.Exists(directoryPath))
            {
                return NotFound("Directory not found");
            }

            // List all image files in the specified directory
            var imageFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => file.EndsWith(".png") || file.EndsWith(".jpg") || file.EndsWith(".jpeg"))
                .Select(file => Path.GetFileName(file))
                .ToList();

            return Ok(imageFiles);
        }

        [HttpGet("GetImage")]
        public IActionResult GetImage(string fileName)
        {
            // Get the current project directory
            string projectDirectory = Directory.GetCurrentDirectory();

            //string filePath = Path.Combine(Path.GetTempPath(), "ExtractedRungs", fileName);
            string filePath = Path.Combine(projectDirectory, fileName);
            //Console.WriteLine("GetImage filepath: " + filePath);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Image not found.");
            }

            byte[] imageBytes = System.IO.File.ReadAllBytes(filePath);
            return File(imageBytes, "image/png");  // Adjust the content type according to the image format
        }

        [HttpDelete("delete-images")]
        public IActionResult DeleteImages()
        {
            try
            {
                // Get the current project directory
                string projectDirectory = Directory.GetCurrentDirectory();

                // Call the function to delete the images without deleting the directory
                _codeAuditorServiceUDT.DeleteImages(projectDirectory);
                return Ok("Images deleted successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting the images: {ex.Message}");
            }
        }

    }

}

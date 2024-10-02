using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;

namespace SAI_OT_Apps.Server.Controllers
{
    public class CodeAuditorController : Controller
    {
        private CodeAuditorService _codeAuditorService;
        public CodeAuditorController()
        {
            _codeAuditorService = new CodeAuditorService(); // Instantiate the service here
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
        //public async Task<IActionResult> UpdateRoutineWithComments([FromQuery] string PLCfilePath, string routineName, List<RungDescription> RoutineDescriptionRevised)
        //{
        //    string result;
        //    try
        //    {
        //        result = _codeAuditorService.UpdateRoutineWithComments(PLCfilePath, routineName, RoutineDescriptionRevised);
        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log the exception (you can use any logging framework)
        //        Console.WriteLine(ex.Message);
        //        return StatusCode(500, "Internal server error: " + ex.Message);
        //    }
        //}
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
        public async Task<IActionResult> SAIDescriptionAnalysis([FromQuery] string routineCode)
        {
            try
            {
                var (preRungAnalysis, rungs) = await _codeAuditorService.SAIDescriptionAnalysis(routineCode);
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
}

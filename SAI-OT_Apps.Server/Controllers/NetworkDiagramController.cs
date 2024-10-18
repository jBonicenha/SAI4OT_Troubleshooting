using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Services;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace SAI_OT_Apps.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NetworkDiagramController : ControllerBase
    {
        private readonly NetworkDiagramService _networkDiagramService;

        // O IConfiguração será injetado aqui
        public NetworkDiagramController(NetworkDiagramService networkDiagramService)
        {
            _networkDiagramService = networkDiagramService; // A injeção de dependência
        }

        //This function analyze the PLC backup files and provide EqpList and EqpConnectionList as output
        [HttpPost("plcAnalysis")]
        public async Task<IActionResult> plcAnalysis([FromQuery] string directoryPLC)
        {
            try
            {
                var (diagrams, eqpConnectionsList, eqpConnectionsListAllRelated) = _networkDiagramService.plcAnalysis(directoryPLC);
                var result = new
                {                    
                    EqpDetailsList = diagrams,
                    EqpConnectionsList = eqpConnectionsList,
                    EqpConnectionsListAllRelated = eqpConnectionsListAllRelated
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

        //Based a list of EqpConnectionList this function generate the XML diagram to be used in Draw.IO application
        [HttpPost("drawioXMLGenerator")]
        public async Task<IActionResult> drawioXMLGenerator([FromQuery] string EqpConnectionList)
        {
            try
            {
                string result = await _networkDiagramService.generateDrawIOXML(EqpConnectionList);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use any logging framework)
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("SAINetworkAnalysis")]
        public async Task<IActionResult> SAINetworkAnalysis([FromQuery] string tableList, string connectionList)
        {
            string result = "";
            try
            {
                result = await _networkDiagramService.SAINetworkAnalysis(tableList, connectionList);
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

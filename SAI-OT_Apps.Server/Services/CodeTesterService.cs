using SAI_OT_Apps.Server.Models;
using OfficeOpenXml;
using Opc.Ua.Client;
using Opc.Ua;
using RestSharp;
using System.Text.Json;



namespace SAI_OT_Apps.Server.Services
{
    public class CodeTesterService
    {
        private readonly string _apiKey;
        public CodeTesterService(IConfiguration configuration)
        {
            _apiKey = configuration["apiKey"];
        }
        // Função auxiliar para atualizar o valor da TAG no OPC
        private static async Task UpdateTagValueInOPC(Session session, NodeId nodeId, string value)
        {
            var writeValue = new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue
                {
                    Value = value == "TRUE" ? true : false
                }
            };

            var nodesToWrite = new WriteValueCollection { writeValue };
            var response = await session.WriteAsync(null, nodesToWrite, CancellationToken.None);

            if (!response.Results.All(r => r == Opc.Ua.StatusCodes.Good))
            {
                throw new Exception("Erro ao escrever valor no OPC Server.");
            }
        }

        // Função para criar a sessão OPC
        private static async Task<Session> CreateSession(ApplicationConfiguration config)
        {
            //var endpointURL = "opc.tcp://127.0.0.1:49320"; // URL do servidor OPC - PC Guilherme
            var endpointURL = "opc.tcp://192.168.226.128:4990/SAI_PLC_UAServer"; //PC Julimar
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            return await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);
        }

        // Função para validar e gerar o JSON a partir da planilha
        public async Task<List<CodeTest>> ValidateAndGenerateJsonFromExcel(IFormFile planilha)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Defina o contexto da licença aqui
            List<CodeTest> codeTests = new List<CodeTest>();  // Lista final de CodeTests
            List<TagTested> tagsTested = new List<TagTested>();  // Lista temporária de TAGs SET
            CodeTest currentTest = null;  // Representa o último CHECK encontrado

            // Configuração do OPC UA
            var config = new ApplicationConfiguration()
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier()
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };

            // Validando a configuração do OPC
            await config.Validate(ApplicationType.Client);

            // Criando a sessão OPC
            var session = await CreateSession(config);

            if (session != null && session.Connected)
            {
                using (var package = new ExcelPackage(planilha.OpenReadStream()))
                {
                    var worksheet = package.Workbook.Worksheets[0]; // Planilha Excel
                    int rowCount = worksheet.Dimension.Rows;

                    // Percorrendo as linhas da planilha
                    for (int row = 2; row <= rowCount; row++)
                    {
                        string function = worksheet.Cells[row, 1].Text.ToUpper(); // FUNCTION
                        string tag = worksheet.Cells[row, 2].Text; // TAG
                        string value = worksheet.Cells[row, 3].Text.ToUpper(); // VALUE

                        if (function != "" || tag != "" || value != "")
                        {

                            // Verifica se a TAG existe no OPC Server
                            //NodeId nodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tag}"); //PC Guilherme
                            NodeId nodeId = new NodeId($"ns=2;s=::[SAI_APP]Program:MainProgram.{tag}"); //PC Julimar
                            DataValue opcValue;

                            try
                            {
                                opcValue = session.ReadValue(nodeId); // Tenta ler o valor da TAG no OPC Server
                            }
                            catch (Exception ex)
                            {
                                // Se houver erro ao ler a TAG, registra o erro completo no resultado
                                tagsTested.Add(new TagTested
                                {
                                    Function = function,
                                    Tag = tag,
                                    Value = value,
                                    Result = $"Error: {ex.Message}" // Inclui a mensagem completa do erro
                                });
                                continue; // Pule para a próxima iteração
                            }

                            // Validação e lógica de SETs e CHECKs
                            if (function == "SET")
                            {
                                // Valida se a TAG no OPC precisa ser atualizada
                                if (opcValue.Value != null && opcValue.Value.ToString().ToUpper() != value)
                                {
                                    await UpdateTagValueInOPC(session, nodeId, value); //Update Tag in Value in OPC
                                    await Task.Delay(500); //Wait for the PLC response
                                    tagsTested.Add(new TagTested
                                    {
                                        Function = function,
                                        Tag = tag,
                                        Value = value,
                                        Result = "UPDATED"
                                    });
                                }
                                else
                                {
                                    tagsTested.Add(new TagTested
                                    {
                                        Function = function,
                                        Tag = tag,
                                        Value = value,
                                        Result = "OK"
                                    });
                                }
                            }
                            else if (function == "CHECK")
                            {
                                // Para o CHECK, cria um CodeTest e associa os SETs anteriores
                                string checkResult = "";
                                if(opcValue.StatusCode == Opc.Ua.StatusCodes.Good)
                                {
                                    bool hasError = tagsTested.Any(tagTested => tagTested.Result.Contains("Error"));
                                    if (hasError)
                                    {
                                        checkResult = "Bad";
                                    }
                                    else
                                    {
                                        checkResult = opcValue.Value.ToString().ToUpper();
                                    }                                    
                                }
                                else
                                {
                                    checkResult = $"Error: {opcValue.StatusCode}";
                                }

                                currentTest = new CodeTest
                                {
                                    Function = "CHECK",
                                    Tag = tag,
                                    Value = value,
                                    Result = checkResult,
                                    TagsTested = new List<TagTested>(tagsTested) // Copia os SETs associados
                                };

                                codeTests.Add(currentTest); // Adiciona o CodeTest ao JSON final
                                tagsTested.Clear(); // Limpa a lista de SETs para o próximo bloco
                            }
                        }
                    }

                    // Se restaram SETs sem um CHECK correspondente, cria um CodeTest final
                    if (tagsTested.Count > 0 && currentTest != null)
                    {
                        var finalTest = new CodeTest
                        {
                            Function = "CHECK",
                            Tag = currentTest.Tag, // Usa a última TAG CHECK
                            Value = "UNKNOWN",
                            Result = "Erro: BadNodeIdUnknown", // Finaliza o resultado como erro para as TAGs restantes
                            TagsTested = new List<TagTested>(tagsTested) // Copia os SETs associados
                        };
                        codeTests.Add(finalTest);
                    }
                }
            }

            // Fecha a sessão OPC
            session?.Close();

            // Retorna o JSON completo
            return codeTests;
        }

        public async Task<string> SAICodeTester(string jsonFromSpreadsheet)
        {
            //string json = JsonSerializer.Serialize(jsonFromSpreadsheet, new JsonSerializerOptions { WriteIndented = true });

            var apiKey = _apiKey;

            var client = new RestClient("https://sai-library.saiapplications.com");
            var request = new RestRequest("api/templates/672a48e4cb38f19ccc7841b2/execute", Method.Post)
                //6717d24cd962ca21467d26a8
                .AddJsonBody(new
                {
                    inputs = new Dictionary<string, string>
                    {
                        ["json"] = jsonFromSpreadsheet // JSON resultante da planilha
                    }
                })
                .AddHeader("X-Api-Key", apiKey);

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                return response.Content; // Aqui pode retornar diretamente a resposta da API
            }
            else
            {
                throw new Exception($"API SAICodeTester failed! Status: {response.StatusCode}, Error: {response.ErrorMessage}");
            }
        }



    }
}

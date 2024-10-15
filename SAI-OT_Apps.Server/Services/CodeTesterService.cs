using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua;


namespace SAI_OT_Apps.Server.Services
{
    public class CodeTesterService
    {
        // Dicionário que vincula TAG_RESULT a grupos de TAGs.
        private static Dictionary<string, List<string>> tagGroups = new Dictionary<string, List<string>>()
        {
            { "TAG_RESULT", new List<string> { "TAG_1", "TAG_2" } },
            { "TAG_RESULT_1", new List<string> { "TAG_3", "TAG_4" } }
            // Adicione mais TAG_RESULTs e seus grupos de TAGs aqui
        };

        public List<CodeTest> ProcessarPlanilha(IFormFile arquivo)
        {
            var codeTests = new List<CodeTest>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Defina o contexto da licença aqui

            // Verifique se o arquivo é um arquivo de planilha
            if (arquivo.ContentType != "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" &&
                arquivo.ContentType != "application/vnd.ms-excel")
            {
                throw new Exception("Tipo de arquivo não suportado.");
            }

            // Caminho temporário para salvar o arquivo
            var caminhoTemporario = Path.Combine(Path.GetTempPath(), arquivo.FileName);

            // Salvar o arquivo no diretório temporário
            using (var stream = new FileStream(caminhoTemporario, FileMode.Create))
            {
                arquivo.CopyTo(stream);
            }

            // Lógica para ler a planilha (usando, por exemplo, EPPlus ou outro pacote)
            using (var package = new ExcelPackage(new FileInfo(caminhoTemporario)))
            {
                var worksheet = package.Workbook.Worksheets[0]; // Primeira aba
                int rowCount = worksheet.Dimension.Rows;

                var currentCodeTest = new CodeTest
                {
                    TagsTested = new List<TagTested>()
                };

                for (int row = 2; row <= rowCount; row++)
                {
                    string function = worksheet.Cells[row, 1].Text; // FUNCTION
                    string tag = worksheet.Cells[row, 2].Text; // TAG
                    string value = worksheet.Cells[row, 3].Text; // VALUE

                    if (function == "SET")
                    {
                        var tagTested = new TagTested
                        {
                            Function = function,
                            Tag = tag,
                            Value = value,
                            Result = "OK" // Result sempre será OK para SET
                        };
                        currentCodeTest.TagsTested.Add(tagTested);
                    }
                    else if (function == "CHECK")
                    {
                        currentCodeTest.Function = function;
                        currentCodeTest.Tag = tag;
                        currentCodeTest.Value = value;
                        currentCodeTest.Result = value.ToUpper() == "TRUE" ? "TRUE" : "FALSE";

                        // Adiciona o CodeTest atual à lista e cria um novo para o próximo bloco
                        codeTests.Add(currentCodeTest);
                        currentCodeTest = new CodeTest { TagsTested = new List<TagTested>() };
                    }
                }
            }

            // Exclua o arquivo temporário se necessário
            System.IO.File.Delete(caminhoTemporario);

            return codeTests;
        }

        //OPC UA Client - Based a tag list, connect to OPC UA Server and read the current values
        async static public Task<string> OPCClient(List<string> tagList)
        {


            // Define the application configuration
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

            // Validate the application configuration
            await config.Validate(ApplicationType.Client);

            // Create an OPC UA application instance.
            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };
            Session session = null;
            string concatTags = "";
            try
            {

                // Conecta ao servidor
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);



                // Create a session with the serverError establishing a connection: BadNotConnected
                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                // Check if the session is connected
                if (session != null && session.Connected)
                {
                    // Read the value of the node
                    for (int i = 0; i < tagList.Count; i++)
                    {
                        var nodeId = new NodeId("ns=2;s=Channel1.CodeTester." + tagList[i]);
                        var value = session.ReadValue(nodeId);
                        string auxTemp = tagList[i].ToString() + " = " + value.Value.ToString();
                        concatTags += auxTemp + "\n";
                    }
                }
                else
                {
                    concatTags = "Error: Not connected to the server.";
                }
                //Close the session
                session.Close();

            }

            catch (Exception ex)
            {
                Console.Error.WriteLine("Ocorreu um erro: " + ex.Message);
                return null;
            }
            finally
            {
                // Close the session if it was created
                session?.Close();
            }

            return concatTags;
        }

        public async static Task<string> OPCWriteClient(Dictionary<string, string> tagValues)
        {
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            string concatResults = "";
            Session session = null;

            try
            {
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    var nodesToWrite = new WriteValueCollection();

                    foreach (var tagValue in tagValues)
                    {
                        // Validação dos valores
                        if (tagValue.Value.ToUpper() != "TRUE" && tagValue.Value.ToUpper() != "FALSE")
                        {
                            return $"Erro: O valor '{tagValue.Value}' da tag '{tagValue.Key}' é inválido. Apenas 'TRUE' ou 'FALSE' são permitidos.";
                        }

                        var writeValue = new WriteValue
                        {
                            NodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tagValue.Key}"),
                            AttributeId = Attributes.Value,
                            Value = new DataValue
                            {
                                Value = tagValue.Value.ToUpper() == "TRUE" ? true : false
                            }
                        };
                        nodesToWrite.Add(writeValue);
                    }

                    var requestHeader = new RequestHeader();
                    // Use CancellationToken.None to avoid the error
                    var response = await session.WriteAsync(requestHeader, nodesToWrite, CancellationToken.None);

                    if (response.Results.All(r => r == Opc.Ua.StatusCodes.Good))
                    {
                        concatResults = "Todos os valores foram escritos com sucesso.";
                    }
                    else
                    {
                        concatResults = "Alguns valores não foram escritos corretamente.";
                    }

                    await session.CloseAsync();
                }
                else
                {
                    concatResults = "Erro: Não foi possível conectar ao servidor OPC.";
                }
            }
            catch (Exception ex)
            {
                concatResults = "Erro ao tentar escrever nas tags: " + ex.Message;
            }
            finally
            {
                session?.Close();
            }

            return concatResults;
        }


        public async static Task<string> UpdateTagResult(Dictionary<string, string> tagValues, string tagResultName)
        {
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            string concatResults = "";
            Session session = null;

            try
            {
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    var nodesToWrite = new WriteValueCollection();
                    bool result = true; // Usado para calcular o valor de TAG_RESULT

                    foreach (var tagValue in tagValues)
                    {
                        // Validação: aceita apenas "TRUE" ou "FALSE"
                        if (tagValue.Value.ToUpper() != "TRUE" && tagValue.Value.ToUpper() != "FALSE")
                        {
                            throw new ArgumentException($"Valor inválido para a tag {tagValue.Key}. Somente 'TRUE' ou 'FALSE' são aceitos.");
                        }

                        var writeValue = new WriteValue
                        {
                            NodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tagValue.Key}"),
                            AttributeId = Attributes.Value,
                            Value = new DataValue
                            {
                                Value = tagValue.Value.ToUpper() == "TRUE" ? true : false
                            }
                        };
                        nodesToWrite.Add(writeValue);

                        // Atualizar o resultado com base no valor da TAG
                        result &= tagValue.Value.ToUpper() == "TRUE";
                    }

                    var writeResponse = await session.WriteAsync(null, nodesToWrite, CancellationToken.None);

                    if (writeResponse.Results.All(r => r == Opc.Ua.StatusCodes.Good))
                    {
                        concatResults = "Tags atualizadas com sucesso. ";
                    }
                    else
                    {
                        concatResults = "Falha ao atualizar algumas Tags. ";
                    }

                    // Agora, atualizamos a TAG_RESULT com o valor calculado
                    var tagResultNode = new WriteValue
                    {
                        NodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tagResultName}"),
                        AttributeId = Attributes.Value,
                        Value = new DataValue
                        {
                            Value = result
                        }
                    };

                    var tagResultWrite = new WriteValueCollection { tagResultNode };
                    var response = await session.WriteAsync(null, tagResultWrite, CancellationToken.None);

                    if (response.Results.All(r => r == Opc.Ua.StatusCodes.Good))
                    {
                        concatResults += $"TAG_RESULT '{tagResultName}' atualizado para: {result}";
                    }
                    else
                    {
                        concatResults += $"Falha ao atualizar TAG_RESULT '{tagResultName}'.";
                    }

                    await session.CloseAsync();
                }
                else
                {
                    concatResults = "Erro: Não foi possível conectar ao servidor OPC.";
                }
            }
            catch (Exception ex)
            {
                concatResults = "Erro ao tentar atualizar as tags: " + ex.Message;
            }
            finally
            {
                session?.Close();
            }

            return concatResults;
        }


        public async static Task<List<CodeTest>> OPCReadAndCheckTagsAsync(Dictionary<string, List<string>> tagsGroupedByResult)
        {
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            var codeTests = new List<CodeTest>();
            Session session = null;

            try
            {
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    foreach (var entry in tagsGroupedByResult)
                    {
                        var tagResult = entry.Key; // TAG_RESULT_X
                        var tagsToTest = entry.Value; // List of TAGs associated with the TAG_RESULT

                        var currentCodeTest = new CodeTest
                        {
                            Function = "CHECK",
                            TagsTested = new List<TagTested>(),
                            Tag = tagResult,
                            Value = "",
                            Result = ""
                        };

                        foreach (var tag in tagsToTest)
                        {
                            var nodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tag}");
                            var value = session.ReadValue(nodeId);

                            currentCodeTest.TagsTested.Add(new TagTested
                            {
                                Function = "SET",
                                Tag = tag,
                                Value = value.Value.ToString().ToUpper() == "TRUE" ? "TRUE" : "FALSE",
                                Result = "OK"
                            });
                        }

                        // Simulação da lógica de resultado para TAG_RESULT
                        var resultNodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tagResult}");
                        var resultValue = session.ReadValue(resultNodeId);

                        currentCodeTest.Value = resultValue.Value.ToString();
                        currentCodeTest.Result = currentCodeTest.Value.ToUpper() == "TRUE" ? "TRUE" : "FALSE";

                        codeTests.Add(currentCodeTest);
                    }
                }
                else
                {
                    throw new Exception("Erro: Não foi possível conectar ao servidor OPC.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao tentar ler as tags: {ex.Message}");
            }
            finally
            {
                session?.Close();
            }

            return codeTests;
        }

        // Função para testar TAGs associadas a um TAG_RESULT e retornar o JSON
        public async static Task<CodeTest> TestTagResult(string tagResultName)
        {
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            Session session = null;
            CodeTest codeTestResult = new CodeTest
            {
                TagsTested = new List<TagTested>()
            };

            try
            {
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    // Verifica se o TAG_RESULT existe no dicionário de grupos
                    if (tagGroups.ContainsKey(tagResultName))
                    {
                        bool finalResult = true;

                        // Percorre todas as TAGs associadas ao TAG_RESULT
                        foreach (var tag in tagGroups[tagResultName])
                        {
                            var nodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tag}");
                            var value = session.ReadValue(nodeId);

                            // Processa o resultado da leitura da TAG
                            var tagTested = new TagTested
                            {
                                Function = "SET",
                                Tag = tag,
                                Value = value.Value.ToString().ToUpper(),
                                Result = "OK" // Consideramos que a leitura sempre será OK, a menos que haja falha de conexão
                            };
                            codeTestResult.TagsTested.Add(tagTested);

                            // Determina o resultado final com base nos valores das TAGs
                            finalResult &= value.Value.ToString().ToUpper() == "TRUE";
                        }

                        // Atualiza a TAG_RESULT com base nas leituras das TAGs associadas
                        codeTestResult.Function = "CHECK";
                        codeTestResult.Tag = tagResultName;
                        codeTestResult.Value = finalResult ? "TRUE" : "FALSE";
                        codeTestResult.Result = finalResult ? "TRUE" : "FALSE";
                    }
                    else
                    {
                        throw new KeyNotFoundException($"TAG_RESULT '{tagResultName}' não encontrado.");
                    }

                    await session.CloseAsync();
                }
                else
                {
                    throw new Exception("Não foi possível conectar ao servidor OPC.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao tentar ler as tags: {ex.Message}");
            }
            finally
            {
                session?.Close();
            }

            return codeTestResult;
        }

        public async static Task<List<CodeTest>> GetAllTagResults()
        {
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            List<CodeTest> tagResultsList = new List<CodeTest>();
            Session session = null;

            try
            {
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    // Supondo que você já tenha uma lista de TAG_RESULTS predefinidos no servidor OPC
                    List<string> tagResults = await GetTagResultsFromServer(session); // Método que busca os TAG_RESULTS existentes

                    foreach (var tagResult in tagResults)
                    {
                        var tagsTested = await GetTagsLinkedToTagResult(session, tagResult); // Método que busca as tags vinculadas ao TAG_RESULT

                        var value = session.ReadValue(new NodeId($"ns=2;s=Channel1.CodeTester.{tagResult}")).Value.ToString();

                        var codeTest = new CodeTest
                        {
                            Function = "CHECK",
                            TagsTested = tagsTested, // Lista de tags testadas vinculadas
                            Tag = tagResult,
                            Value = value,
                            Result = value.ToUpper() == "TRUE" ? "TRUE" : "FALSE"
                        };

                        tagResultsList.Add(codeTest);
                    }

                    await session.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro: " + ex.Message);
            }
            finally
            {
                session?.Close();
            }

            return tagResultsList;
        }

        public async static Task<List<CodeTest>> GetAllTagResultsByQuery()
        {
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            List<CodeTest> tagResultsList = new List<CodeTest>();
            Session session = null;

            try
            {
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    // Supondo que você já tenha uma lista de TAG_RESULTS predefinidos no servidor OPC
                    List<string> tagResults = await GetTagResultsFromServer(session); // Método que busca os TAG_RESULTS existentes

                    foreach (var tagResult in tagResults)
                    {
                        var tagsTested = await GetTagsLinkedToTagResult(session, tagResult); // Método que busca as tags vinculadas ao TAG_RESULT

                        var value = session.ReadValue(new NodeId($"ns=2;s=Channel1.CodeTester.{tagResult}")).Value.ToString();

                        var codeTest = new CodeTest
                        {
                            Function = "CHECK",
                            TagsTested = tagsTested, // Lista de tags testadas vinculadas
                            Tag = tagResult,
                            Value = value,
                            Result = value.ToUpper() == "TRUE" ? "TRUE" : "FALSE"
                        };

                        tagResultsList.Add(codeTest);
                    }

                    await session.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro: " + ex.Message);
            }
            finally
            {
                session?.Close();
            }

            return tagResultsList;
        }

        private static async Task<List<string>> GetTagResultsFromServer(Session session)
        {
            // Lógica para buscar os TAG_RESULTS do servidor OPC
            // Este é apenas um exemplo; você precisaria personalizar conforme a estrutura do seu servidor OPC
            return new List<string> { "TAG_RESULT", "TAG_RESULT_1", "TAG_RESULT_3" };
        }

        private static async Task<List<TagTested>> GetTagsLinkedToTagResult(Session session, string tagResult)
        {
            // Lógica para buscar as tags vinculadas a um TAG_RESULT
            // Exemplo fictício de como mapear tags vinculadas
            var tagsLinked = new List<TagTested>
        {
            new TagTested { Function = "SET", Tag = "TAG_1", Value = "TRUE", Result = "OK" },
            new TagTested { Function = "SET", Tag = "TAG_2", Value = "FALSE", Result = "OK" }
        };
            return tagsLinked;
        }

        public async static Task<List<ExcelRowResult>> ProcessExcelFile(IFormFile arquivo)
        {
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            Session session = null;
            var results = new List<ExcelRowResult>();

            try
            {
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    // Open the Excel file and start processing
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(arquivo.OpenReadStream()))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;

                        // Start looping through the rows
                        for (int row = 2; row <= rowCount; row++)
                        {
                            string function = worksheet.Cells[row, 1].Text;
                            string tag = worksheet.Cells[row, 2].Text;
                            string value = worksheet.Cells[row, 3].Text;
                            string result = "";

                            if (function == "SET")
                            {
                                // Check if TAG exists in OPC
                                var nodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tag}");
                                try
                                {
                                    var opcValue = session.ReadValue(nodeId).Value.ToString().ToUpper();
                                    result = (opcValue == value.ToUpper()) ? "OK" : $"Mismatch: expected {value}, got {opcValue}";
                                }
                                catch (Exception ex)
                                {
                                    result = "NOT FOUND";
                                }
                            }
                            else if (function == "CHECK")
                            {
                                // Check TAG_RESULT in OPC
                                var nodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tag}");
                                try
                                {
                                    var opcValue = session.ReadValue(nodeId).Value.ToString().ToUpper();
                                    result = opcValue == "TRUE" ? "TRUE" : "FALSE";
                                }
                                catch (Exception ex)
                                {
                                    result = "NOT FOUND";
                                }
                            }

                            // Add to results list
                            results.Add(new ExcelRowResult
                            {
                                Function = function,
                                Tag = tag,
                                Value = value,
                                Result = result
                            });
                        }
                    }

                    await session.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Erro ao tentar processar a planilha: " + ex.Message);
            }
            finally
            {
                session?.Close();
            }

            return results;
        }

        public async static Task<string> PopulateExcelFromOPC(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Defina o contexto da licença aqui
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Defina o contexto da licença aqui
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            Session session = null;
            try
            {
                // Conectar ao OPC Server
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    // Tags que queremos ler do OPC (essa lista seria gerada dinamicamente)
                    List<string> tagList = new List<string> { "TAG_1", "TAG_2", "TAG_RESULT_1" };

                    using (ExcelPackage package = new ExcelPackage())
                    {
                        // Cria uma nova planilha
                        var worksheet = package.Workbook.Worksheets.Add("OPC Data");
                        worksheet.Cells[1, 1].Value = "FUNCTION";
                        worksheet.Cells[1, 2].Value = "TAG";
                        worksheet.Cells[1, 3].Value = "VALUE";
                        worksheet.Cells[1, 4].Value = "RESULT";

                        int row = 2; // Começando da linha 2 (depois do cabeçalho)

                        foreach (var tag in tagList)
                        {
                            var nodeId = new NodeId("ns=2;s=Channel1.CodeTester." + tag);
                            var value = session.ReadValue(nodeId);

                            // Determina a função baseada no nome da TAG
                            string function = tag.StartsWith("TAG_RESULT") ? "CHECK" : "SET";
                            string result;

                            if (value.StatusCode == Opc.Ua.StatusCodes.Good)
                            {
                                if (function == "SET")
                                {
                                    result = "OK"; // Apenas "OK" para SET
                                }
                                else // FUNCTION é "CHECK"
                                {
                                    // Verifica se o valor lido é TRUE ou FALSE
                                    result = value.Value != null && value.Value.ToString().ToUpper() == "TRUE" ? "TRUE" : "FALSE";
                                }
                            }
                            else
                            {
                                result = "NOT FOUND"; // Se a TAG não for encontrada
                            }

                            // Adiciona os dados na planilha
                            worksheet.Cells[row, 1].Value = function;
                            worksheet.Cells[row, 2].Value = tag;
                            worksheet.Cells[row, 3].Value = value.Value != null ? value.Value.ToString() : "N/A"; // O valor lido
                            worksheet.Cells[row, 4].Value = result; // O resultado

                            row++;
                        }

                        // Salvar no diretório temporário do sistema
                        var tempPath = Path.GetTempPath();
                        var tempFilePath = Path.Combine(tempPath, "CodeTester2.xlsx");
                        package.SaveAs(new FileInfo(tempFilePath));

                        return $"Planilha populada com sucesso em: {tempFilePath}";
                    }
                }
                else
                {
                    return "Erro: Não foi possível conectar ao servidor OPC.";
                }
            }
            catch (Exception ex)
            {
                return "Erro ao tentar buscar dados do OPC: " + ex.Message;
            }
            finally
            {
                session?.Close();
            }

        }

        public async static Task<string> GetAllTagsFromOPCAndWriteToExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Defina o contexto da licença aqui
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

            await config.Validate(ApplicationType.Client);

            var application = new ApplicationInstance
            {
                ApplicationName = "OPCUAClient",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };

            Session session = null;

            try
            {
                // Conectar ao OPC Server
                var endpointURL = "opc.tcp://127.0.0.1:49320";
                var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
                var endpointConfiguration = EndpointConfiguration.Create(config);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

                session = await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);

                if (session != null && session.Connected)
                {
                    Console.WriteLine("Conexão com o servidor OPC foi bem-sucedida.");

                    // Chama o método para buscar apenas as TAGs específicas
                    List<string> tagList = await GetSpecificOpcNodes(session, new NodeId("ns=2;s=Channel1.CodeTester"));

                    using (ExcelPackage package = new ExcelPackage())
                    {
                        // Cria uma nova planilha
                        var worksheet = package.Workbook.Worksheets.Add("OPC Data");
                        worksheet.Cells[1, 1].Value = "FUNCTION";
                        worksheet.Cells[1, 2].Value = "TAG";
                        worksheet.Cells[1, 3].Value = "VALUE";
                        worksheet.Cells[1, 4].Value = "RESULT";

                        int row = 2; // Começando da linha 2 (depois do cabeçalho)

                        foreach (var tag in tagList)
                        {
                            try
                            {
                                var nodeId = new NodeId(tag);
                                var value = session.ReadValue(nodeId);

                                // Verifica se o valor foi lido corretamente
                                string function = tag.Contains("TAG_RESULT") ? "CHECK" : "SET";
                                string result;

                                if (value.StatusCode == Opc.Ua.StatusCodes.Good)
                                {
                                    if (function == "SET")
                                    {
                                        result = "OK"; // Apenas "OK" para SET
                                    }
                                    else // FUNCTION é "CHECK"
                                    {
                                        result = value.Value != null && value.Value.ToString().ToUpper() == "TRUE" ? "TRUE" : "FALSE";
                                    }

                                    Console.WriteLine($"TAG {tag} encontrada com valor {value.Value}, inserindo na planilha.");
                                }
                                else
                                {
                                    result = "NOT FOUND"; // Se a TAG não for encontrada
                                    Console.WriteLine($"TAG {tag} não foi encontrada no OPC.");
                                }

                                // Remover o prefixo "ns=2;s=Channel1.CodeTester." da TAG
                                string tagName = tag.Replace("ns=2;s=Channel1.CodeTester.", "");

                                // Converter o valor para uppercase e adicioná-lo na planilha
                                worksheet.Cells[row, 1].Value = function;
                                worksheet.Cells[row, 2].Value = tagName; // Exibe apenas o nome da TAG
                                worksheet.Cells[row, 3].Value = value.Value != null ? value.Value.ToString().ToUpper() : "N/A"; // O valor lido em uppercase
                                worksheet.Cells[row, 4].Value = result; // O resultado

                                row++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar a TAG {tag}: {ex.Message}");
                            }
                        }

                        // Salvar no diretório temporário do sistema
                        var tempPath = Path.GetTempPath();
                        var tempFilePath = Path.Combine(tempPath, "CodeTester2.xlsx");
                        package.SaveAs(new FileInfo(tempFilePath));

                        Console.WriteLine($"Planilha populada com sucesso em: {tempFilePath}");
                        return $"Planilha populada com sucesso em: {tempFilePath}";
                    }
                }
                else
                {
                    Console.WriteLine("Erro: Não foi possível conectar ao servidor OPC.");
                    return "Erro: Não foi possível conectar ao servidor OPC.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tentar buscar dados do OPC: {ex.Message}");
                return $"Erro ao tentar buscar dados do OPC: {ex.Message}";
            }
            finally
            {
                session?.Close();
            }
        }



        private static List<ReferenceDescription> Browse(Session session, NodeId nodeId)
        {
            // Define o request para navegar pelos nós
            BrowseDescription nodeToBrowse = new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = (uint)NodeClass.Object | (uint)NodeClass.Variable,
                ResultMask = (uint)BrowseResultMask.All
            };

            // Faz a requisição de navegação
            BrowseResultCollection browseResults;
            DiagnosticInfoCollection diagnosticInfos;
            session.Browse(
                null,
                null,
                0,
                new BrowseDescriptionCollection { nodeToBrowse },
                out browseResults,
                out diagnosticInfos
            );

            // Verifica os resultados
            if (browseResults != null && browseResults.Count > 0)
            {
                return browseResults[0].References.ToList(); // Retorna a lista de referências
            }

            return new List<ReferenceDescription>(); // Se não houver resultados, retorna uma lista vazia
        }


        // Função para buscar todas as TAGs (Nodes) do OPC Server
        // Função para buscar todas as TAGs (Nodes) do OPC Server de forma assíncrona
        private static async Task<List<string>> GetSpecificOpcNodes(Session session, NodeId nodeId)
        {
            List<string> tagList = new List<string>();

            // Solicita os nós filhos do nó atual
            var references = Browse(session, nodeId);

            foreach (var reference in references)
            {
                var childNodeId = ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);

                if (childNodeId != null)
                {
                    // Se for um nó variável e estiver dentro do projeto, adiciona na lista de TAGs
                    if (reference.NodeClass == NodeClass.Variable)
                    {
                        string tagName = reference.DisplayName.Text;
                        // Verifica se a TAG está no caminho especificado
                        if (tagName.StartsWith("TAG_") || tagName.StartsWith("TAG_RESULT_"))
                        {
                            tagList.Add(childNodeId.ToString());
                            Console.WriteLine($"TAG {tagName} encontrada.");
                        }
                    }

                    // Recorre sobre nós filhos apenas se a referência não for uma TAG
                    if (reference.NodeClass == NodeClass.Object || reference.NodeClass == NodeClass.Variable)
                    {
                        var childTags = await GetSpecificOpcNodes(session, childNodeId);
                        tagList.AddRange(childTags);
                    }
                }
            }

            return tagList;
        }

    }
}

﻿using Microsoft.AspNetCore.Mvc;
using SAI_OT_Apps.Server.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua;
using Newtonsoft.Json;
using System.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Opc.Ua;
using Opc.Ua.Client;


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
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
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

                    // Navega até o "Channel1"
                    var channelNode = GetNodeIdByDisplayName(session, ObjectIds.ObjectsFolder, "Channel1");
                    if (channelNode == null) throw new Exception("Channel1 node não encontrado.");

                    // Navega até o "CodeTester" dentro do Channel1
                    var codeTesterNode = GetNodeIdByDisplayName(session, channelNode, "CodeTester");
                    if (codeTesterNode == null) throw new Exception("CodeTester node não encontrado.");

                    // Agora que estamos no "CodeTester", buscamos todas as TAGs
                    List<string> allTags = GetAllOpcNodes(session, codeTesterNode);

                    // Separar TAGs SET e CHECK
                    List<string> setTags = allTags.Where(t => !t.Contains("TAG_RESULT")).ToList(); // TAGs SET
                    List<string> checkTags = allTags.Where(t => t.Contains("TAG_RESULT")).ToList(); // TAGs CHECK

                    using (ExcelPackage package = new ExcelPackage())
                    {
                        // Cria uma nova planilha
                        var worksheet = package.Workbook.Worksheets.Add("OPC Data");
                        worksheet.Cells[1, 1].Value = "FUNCTION";
                        worksheet.Cells[1, 2].Value = "TAG";
                        worksheet.Cells[1, 3].Value = "VALUE";
                        worksheet.Cells[1, 4].Value = "RESULT";

                        int row = 2; // Começando da linha 2 (depois do cabeçalho)

                        // Variáveis de controle
                        int setIndex = 0;
                        int checkIndex = 0;

                        // Processa a cada 2 TAGs de SET, uma de CHECK
                        while (setIndex < setTags.Count || checkIndex < checkTags.Count)
                        {
                            // Adiciona até 2 TAGs de SET
                            for (int i = 0; i < 2 && setIndex < setTags.Count; i++, setIndex++)
                            {
                                var tag = setTags[setIndex];
                                var nodeId = new NodeId(tag);
                                var value = session.ReadValue(nodeId);

                                string result = value.StatusCode == Opc.Ua.StatusCodes.Good ? "OK" : "NOT FOUND";
                                worksheet.Cells[row, 1].Value = "SET";
                                worksheet.Cells[row, 2].Value = tag.Split('.').Last(); // Pegando o nome simplificado da TAG
                                worksheet.Cells[row, 3].Value = value.Value != null ? value.Value.ToString().ToUpper() : "N/A";
                                worksheet.Cells[row, 4].Value = result;

                                row++;
                            }

                            // Adiciona 1 TAG de CHECK
                            if (checkIndex < checkTags.Count)
                            {
                                var checkTag = checkTags[checkIndex++];
                                var checkNodeId = new NodeId(checkTag);
                                var checkValue = session.ReadValue(checkNodeId);

                                string checkResult = checkValue.StatusCode == Opc.Ua.StatusCodes.Good ?
                                    (checkValue.Value != null && checkValue.Value.ToString().ToUpper() == "TRUE" ? "TRUE" : "FALSE")
                                    : "NOT FOUND";

                                worksheet.Cells[row, 1].Value = "CHECK";
                                worksheet.Cells[row, 2].Value = checkTag.Split('.').Last();
                                worksheet.Cells[row, 3].Value = checkValue.Value != null ? checkValue.Value.ToString().ToUpper() : "N/A";
                                worksheet.Cells[row, 4].Value = checkResult;

                                row++;
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

        // Função para buscar NodeId por DisplayName
        private static NodeId GetNodeIdByDisplayName(Session session, NodeId parentNodeId, string displayName)
        {
            BrowseDescription nodeToBrowse = new BrowseDescription
            {
                NodeId = parentNodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = (uint)NodeClass.Object | (uint)NodeClass.Variable,
                ResultMask = (uint)BrowseResultMask.All
            };

            BrowseResultCollection results;
            DiagnosticInfoCollection diagnosticInfos;
            session.Browse(null, null, 0, new BrowseDescriptionCollection { nodeToBrowse }, out results, out diagnosticInfos);

            foreach (var result in results)
            {
                foreach (var reference in result.References)
                {
                    if (reference.DisplayName.Text == displayName)
                    {
                        return ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);
                    }
                }
            }

            return null;
        }

        // Função para buscar todas as TAGs (Nodes) dentro de "CodeTester"
        private static List<string> GetAllOpcNodes(Session session, NodeId nodeId)
        {
            List<string> tagList = new List<string>();

            BrowseDescription nodeToBrowse = new BrowseDescription
            {
                NodeId = nodeId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = (uint)NodeClass.Variable,
                ResultMask = (uint)BrowseResultMask.All
            };

            BrowseResultCollection results;
            DiagnosticInfoCollection diagnosticInfos;
            session.Browse(null, null, 0, new BrowseDescriptionCollection { nodeToBrowse }, out results, out diagnosticInfos);

            foreach (var result in results)
            {
                foreach (var reference in result.References)
                {
                    var childNodeId = ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);
                    if (childNodeId != null && reference.NodeClass == NodeClass.Variable)
                    {
                        tagList.Add(childNodeId.ToString());
                        Console.WriteLine($"TAG {reference.DisplayName.Text} encontrada.");
                    }
                }
            }

            return tagList;
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

        public static ApplicationConfiguration SetupOPCUAConfiguration()
        {
            var config = new ApplicationConfiguration()
            {
                ApplicationName = "MyOPCClient",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                ClientConfiguration = new ClientConfiguration()
                {
                    DefaultSessionTimeout = 60000
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas()
                {
                    OperationTimeout = 15000
                },
                ServerConfiguration = new ServerConfiguration()
                {
                    MinRequestThreadCount = 5
                }
            };

            config.Validate(ApplicationType.Client).GetAwaiter().GetResult();

            return config;
        }

        public static async Task<Session> CreateSession(ApplicationConfiguration config, string endpointUrl)
        {
            EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
            EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(config);

            var endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
            var session = await Session.Create(config, endpoint, false, "MySession", 60000, null, null);

            return session;
        }
        //ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Defina o contexto da licença aqui
        public async static Task<List<CodeTest>> ValidateAndGenerateJsonFromExcel(IFormFile planilha)
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

                        // Verifica se a TAG existe no OPC Server
                        NodeId nodeId = new NodeId($"ns=2;s=Channel1.CodeTester.{tag}");
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
                                Result = $"Erro: {ex.Message}" // Inclui a mensagem completa do erro
                            });

                            continue; // Pule para a próxima iteração
                        }

                        // Validação e lógica de SETs e CHECKs
                        if (function == "SET")
                        {
                            // Valida se a TAG no OPC precisa ser atualizada
                            if (opcValue.Value != null && opcValue.Value.ToString().ToUpper() != value)
                            {
                                await UpdateTagValueInOPC(session, nodeId, value); // Atualiza o valor da TAG no OPC
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
                            currentTest = new CodeTest
                            {
                                Function = "CHECK",
                                Tag = tag,
                                Value = opcValue.Value?.ToString().ToUpper() ?? "UNKNOWN", // Valor lido do OPC
                                Result = opcValue.StatusCode == Opc.Ua.StatusCodes.Good ? "TRUE" : "Erro", // Ajuste na saída do result
                                TagsTested = new List<TagTested>(tagsTested) // Copia os SETs associados
                            };

                            // Remove a duplicata de CHECK do resultado
                            codeTests.Add(currentTest); // Adiciona o CodeTest ao JSON final
                            tagsTested.Clear(); // Limpa a lista de SETs para o próximo bloco
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
                            Result = "Erro", // Finaliza o resultado como erro para as TAGs restantes
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


        private static DataTable ReadExcelFile(IFormFile file)
        {
            using (var package = new ExcelPackage(file.OpenReadStream()))
            {
                var worksheet = package.Workbook.Worksheets[0]; // Pega a primeira aba
                var rowCount = worksheet.Dimension.Rows;
                var colCount = worksheet.Dimension.Columns;

                // Cria um DataTable para armazenar os dados
                var dataTable = new DataTable();

                // Adiciona as colunas ao DataTable
                for (int col = 1; col <= colCount; col++)
                {
                    dataTable.Columns.Add(worksheet.Cells[1, col].Text); // Usa a primeira linha como cabeçalho
                }

                // Adiciona as linhas ao DataTable
                for (int row = 2; row <= rowCount; row++) // Começando da segunda linha
                {
                    var newRow = dataTable.NewRow();
                    for (int col = 1; col <= colCount; col++)
                    {
                        newRow[col - 1] = worksheet.Cells[row, col].Text;
                    }
                    dataTable.Rows.Add(newRow);
                }

                return dataTable;
            }
        }


        private static async Task<Session> CreateOpcSession(string endpointUrl)
        {
            var application = new ApplicationInstance
            {
                ApplicationName = "YourApplicationName",
                ApplicationType = ApplicationType.Client
            };

            // Configura o aplicativo
            await application.CheckApplicationInstanceCertificate(false, 0);

            // Cria um endpoint para a conexão
            var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: true);

            // Cria a sessão
            return await Session.Create(
                application.ApplicationConfiguration,
                new ConfiguredEndpoint(null, endpoint),
                false,
                false,
                application.ApplicationName,
                60000, // Tempo limite em milissegundos
                null,
                null);
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
            var endpointURL = "opc.tcp://127.0.0.1:49320"; // URL do servidor OPC
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, useSecurity: false);
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            return await Session.Create(config, endpoint, false, "OPCUAClient", 60000, null, null);
        }





    }
}

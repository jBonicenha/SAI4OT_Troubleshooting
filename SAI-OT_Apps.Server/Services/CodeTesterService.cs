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
                        concatResults = "Tags atualizadas com sucesso.\n";
                    }
                    else
                    {
                        concatResults = "Falha ao atualizar algumas Tags.\n";
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

    }
}

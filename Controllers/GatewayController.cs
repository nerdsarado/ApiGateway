using GatewayAPI.Interfaces;
using GatewayAPI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;

namespace GatewayAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GatewayController : ControllerBase
    {
        private readonly IServiceRouter _serviceRouter;
        private readonly ILogger<GatewayController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public GatewayController(
            IServiceRouter serviceRouter,
            ILogger<GatewayController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _serviceRouter = serviceRouter;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("route")]
        public async Task<IActionResult> RouteRequest()
        {
            try
            {
                _logger.LogInformation("📥 Recebendo requisição no gateway...");

                // Ler o corpo da requisição
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(json))
                {
                    return BadRequest(new { sucesso = false, mensagem = "Corpo da requisição vazio" });
                }

                // Desserializar
                var request = JsonSerializer.Deserialize<GatewayRequest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // ⭐⭐ VALIDAÇÃO: Verificar se deserializou corretamente
                if (request == null)
                {
                    return BadRequest(new { sucesso = false, mensagem = "Request inválido" });
                }

                // ⭐⭐ VALIDAÇÃO: Preencher endpoint se vier da URL
                if (string.IsNullOrEmpty(request.Endpoint))
                {
                    // Tenta extrair do caminho da URL
                    var path = Request.Path.Value ?? "";
                    if (path.StartsWith("/api/gateway/"))
                    {
                        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 2)
                        {
                            request.Service = parts[2]; // Ex: "ProgramaProdutos"

                            // Reconstruir endpoint
                            if (parts.Length > 3)
                            {
                                request.Endpoint = string.Join("/", parts.Skip(3));
                            }
                        }
                    }
                }

                _logger.LogInformation("🔀 Processando: Service={Service}, Endpoint={Endpoint}",
                    request.Service, request.Endpoint);

                // Rotear a requisição
                var response = await _serviceRouter.RouteRequestAsync(request);

                // Retornar resposta
                if (response.Success)
                {
                    return StatusCode(response.StatusCode, response.Data);
                }
                else
                {
                    return StatusCode(response.StatusCode, new
                    {
                        sucesso = false,
                        mensagem = response.ErrorMessage,
                        statusCode = response.StatusCode
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro no gateway");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = $"Erro interno: {ex.Message}"
                });
            }
        }

        [HttpPost("{service}/{*endpoint}")]
        public async Task<IActionResult> RouteDynamic(string service, string endpoint)
        {
            try
            {
                _logger.LogInformation("📥 Recebendo requisição dinâmica: {Service}/{Endpoint}", service, endpoint);

                // Ler o corpo da requisição
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();

                object? data = null;
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        data = JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch
                    {
                        data = json;
                    }
                }

                // Criar GatewayRequest
                var request = new GatewayRequest
                {
                    Service = service,
                    Endpoint = endpoint ?? string.Empty, // ⭐ Garantir não nulo
                    HttpMethod = Request.Method,
                    Data = data,
                    Headers = Request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                };

                // Adicionar query parameters
                if (Request.Query.Any())
                {
                    request.QueryParams = Request.Query.ToDictionary(q => q.Key, q => string.Join(",", q.Value));
                }

                _logger.LogInformation("🔀 Roteando dinamicamente: {Service}/{Endpoint}", service, endpoint);

                // Rotear a requisição
                var response = await _serviceRouter.RouteRequestAsync(request);

                // Retornar resposta
                if (response.Success)
                {
                    if (response.Headers != null)
                    {
                        foreach (var header in response.Headers)
                        {
                            Response.Headers[header.Key] = header.Value;
                        }
                    }
                    return StatusCode(response.StatusCode, response.Data);
                }
                else
                {
                    return StatusCode(response.StatusCode, new
                    {
                        sucesso = false,
                        mensagem = response.ErrorMessage,
                        statusCode = response.StatusCode
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro no gateway dinâmico");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = $"Erro interno: {ex.Message}"
                });
            }
        }
        [HttpPost("clientes/cadastrar")]
        public async Task<IActionResult> CadastrarCliente([FromBody] GatewayClienteRequest request)
        {
            try
            {
                _logger.LogInformation("📥 [GATEWAY] Recebendo requisição para cadastro de cliente: {CodigoTarefa}",
                    request?.CodigoTarefa ?? "NÃO INFORMADO");

                // Roteia para o serviço de clientes na porta 6003
                var client = _httpClientFactory.CreateClient("ClienteService");

                // ✅ CORREÇÃO: Usando PostAsJsonAsync corretamente (sem dynamic)
                var response = await client.PostAsJsonAsync("/api/clientes/cadastrar", request);

                if (response.IsSuccessStatusCode)
                {
                    var resultado = await response.Content.ReadFromJsonAsync<object>();
                    return Ok(resultado);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ [GATEWAY] Erro no serviço de clientes: {Error}", errorContent);
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = $"Erro no serviço de clientes: {errorContent}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [GATEWAY] Erro ao processar cadastro de cliente");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = $"Erro interno: {ex.Message}"
                });
            }
        }
        [HttpPost("tarefa")]
        public async Task<IActionResult> ProcessarTarefa()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();

                _logger.LogInformation("📥 [GATEWAY] JSON recebido: {Json}", json);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("codigoTarefa", out var codigoTarefaProp) ||
                    !root.TryGetProperty("tipoTarefa", out var tipoTarefaProp))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Campos obrigatórios ausentes",
                        camposObrigatorios = new[] { "codigoTarefa", "tipoTarefa" }
                    });
                }

                var codigoTarefa = codigoTarefaProp.GetString() ?? string.Empty;
                var tipoTarefa = tipoTarefaProp.GetString() ?? string.Empty;

                JsonElement dadosProp = default;
                var temDados = root.TryGetProperty("dados", out dadosProp);

                _logger.LogInformation("🔍 [GATEWAY] Processando: {CodigoTarefa} - {TipoTarefa}",
                    codigoTarefa, tipoTarefa);

                string targetUrl = tipoTarefa.ToUpper() switch
                {
                    "PRODUTO" => "http://localhost:6001/api/produtos/cadastrar",
                    "FORNECEDOR" => "http://localhost:6002/api/fornecedores/cadastrar",
                    "CLIENTE" => "http://localhost:6003/api/clientes/cadastrar",
                    "DATASHEET" => "http://localhost:6004/api/datasheet/processar",
                    _ => throw new ArgumentException($"Tipo de tarefa inválido: {tipoTarefa}")
                };

                var payload = new
                {
                    codigoTarefa = codigoTarefa,
                    dados = temDados ? dadosProp : (object)new { }
                };

                _logger.LogInformation("🔀 [GATEWAY] Encaminhando para: {Url}", targetUrl);

                // ⭐⭐⭐ CRIAR CLIENTE COM TIMEOUT CORRETO ⭐⭐⭐
                // Usar SocketsHttpHandler para .NET 5+ / .NET Core
                var handler = new SocketsHttpHandler
                {
                    // ⭐ Tempo máximo de conexão
                    ConnectTimeout = TimeSpan.FromMinutes(5),

                    // ⭐ Tempo máximo de inatividade
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),

                    // ⭐ Tempo máximo de vida da conexão
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),

                    // ⭐ Máximo de conexões simultâneas
                    MaxConnectionsPerServer = 20,

                    // ⭐ Timeout para leitura/escrita (opcional)
                    // Nota: Isso é gerenciado pelo HttpClient.Timeout
                };

                using var client = new HttpClient(handler)
                {
                    // ⭐ TIMEOUT PRINCIPAL - 60 minutos
                    Timeout = TimeSpan.FromMinutes(60)
                };

                // Headers padrão
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "GatewayAPI/1.0");

                // ⭐ Usar CancellationTokenSource com timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(60));

                // Enviar requisição
                var response = await client.PostAsJsonAsync(targetUrl, payload, cts.Token);
                var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ [GATEWAY] Tarefa processada com sucesso: {CodigoTarefa}", codigoTarefa);

                    return Ok(new
                    {
                        sucesso = true,
                        codigoTarefa = codigoTarefa,
                        tipoTarefa = tipoTarefa,
                        servicoDestino = targetUrl,
                        gatewayProcessado = true,
                        dataProcessamento = DateTime.UtcNow,
                        respostaServico = responseContent
                    });
                }
                else
                {
                    _logger.LogError("❌ [GATEWAY] Erro no serviço destino: {StatusCode}", response.StatusCode);

                    return StatusCode((int)response.StatusCode, new
                    {
                        sucesso = false,
                        codigoTarefa = codigoTarefa,
                        tipoTarefa = tipoTarefa,
                        servicoDestino = targetUrl,
                        mensagem = $"Erro no serviço destino: {response.StatusCode}",
                        detalhes = responseContent,
                        gatewayProcessado = true
                    });
                }
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken.IsCancellationRequested)
            {
                _logger.LogError(oce, "⏰ [GATEWAY] Timeout - A operação excedeu o tempo limite");
                return StatusCode(504, new
                {
                    sucesso = false,
                    mensagem = "Tempo limite excedido",
                    detalhes = oce.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 [GATEWAY] Erro interno");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro interno no gateway",
                    detalhes = ex.Message
                });
            }
        }
        [HttpPost("teste")]
        public IActionResult TesteEndpoint([FromBody] object qualquerCoisa)
        {
            _logger.LogInformation("🧪 Teste endpoint chamado");

            // Retornar TUDO que chegou
            return Ok(new
            {
                mensagem = "Endpoint de teste funcionando",
                timestamp = DateTime.UtcNow,
                recebido = qualquerCoisa,
                headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                contentType = Request.ContentType,
                contentLength = Request.ContentLength
            });
        }
        [HttpPost("callback")]
        public async Task<IActionResult> Callback([FromBody] object resultado)
        {
            _logger.LogInformation("📥 [GATEWAY] Callback recebido do Datasheet Service");

            try
            {
                // Desserializar para ver o conteúdo
                var json = JsonSerializer.Serialize(resultado);
                _logger.LogInformation("📦 Conteúdo do callback: {Json}", json);

                // Extrair informações importantes
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string codigoTarefa = "";
                string status = "";
                bool sucesso = false;

                if (root.TryGetProperty("codigoTarefa", out var codigoProp))
                    codigoTarefa = codigoProp.GetString() ?? "";

                if (root.TryGetProperty("status", out var statusProp))
                    status = statusProp.GetString() ?? "";

                if (root.TryGetProperty("sucesso", out var sucessoProp))
                    sucesso = sucessoProp.GetBoolean();

                _logger.LogInformation("📊 Callback processado: {CodigoTarefa} - {Status} - Sucesso: {Sucesso}",
                    codigoTarefa, status, sucesso);

                // Aqui você poderia:
                // 1. Atualizar status no banco de dados
                // 2. Enviar notificação para o bot
                // 3. Processar os arquivos gerados

                return Ok(new
                {
                    sucesso = true,
                    mensagem = "Callback recebido com sucesso",
                    codigoTarefa = codigoTarefa,
                    recebidoEm = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar callback");
                return BadRequest(new
                {
                    sucesso = false,
                    mensagem = "Erro ao processar callback",
                    erro = ex.Message
                });
            }
        }

        // Métodos auxiliares
        private object PreparePayloadForService(GatewayTarefaRequest tarefa)
        {
            return tarefa.TipoTarefa.ToUpper() switch
            {
                "PRODUTO" => new
                {
                    codigoTarefa = tarefa.CodigoTarefa,
                    dados = new
                    {
                        descricao = GetStringValue(tarefa.Dados, "descricao"),
                        ncm = GetStringValue(tarefa.Dados, "ncm"),
                        custo = GetDecimalValue(tarefa.Dados, "custo")
                    }
                },
                "FORNECEDOR" => new
                {
                    codigoTarefa = tarefa.CodigoTarefa,
                    dados = new
                    {
                        cnpj = GetStringValue(tarefa.Dados, "cnpj")
                    }
                },
                "CLIENTE" => new
                {
                    codigoTarefa = tarefa.CodigoTarefa,
                    dados = new
                    {
                        cnpj = GetStringValue(tarefa.Dados, "cnpj"),
                        inscricaoEstadual = GetStringValue(tarefa.Dados, "inscricaoEstadual")
                    }
                },
                _ => new { codigoTarefa = tarefa.CodigoTarefa, dados = tarefa.Dados }
            };
        }

        private string GetStringValue(Dictionary<string, object> dados, string key)
        {
            return dados.ContainsKey(key) ? dados[key]?.ToString() ?? string.Empty : string.Empty;
        }

        private decimal GetDecimalValue(Dictionary<string, object> dados, string key)
        {
            if (dados.ContainsKey(key) && decimal.TryParse(dados[key]?.ToString(), out var value))
                return value;
            return 0;
        }

        private string ExtractGeneratedCode(JsonElement jsonElement, string tipoTarefa)
        {
            var propertyName = tipoTarefa.ToUpper() switch
            {
                "PRODUTO" => "codigoProduto",
                "FORNECEDOR" => "codigoFornecedor",
                "CLIENTE" => "codigoCliente",
                _ => null
            };

            if (propertyName != null && jsonElement.TryGetProperty(propertyName, out var codigoProp))
                return codigoProp.GetString() ?? string.Empty;

            // Tentar propriedades alternativas
            var alternativeNames = new[] { "codigo", "id", "numero", "codigoGerado" };
            foreach (var altName in alternativeNames)
            {
                if (jsonElement.TryGetProperty(altName, out var altProp))
                    return altProp.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private string ExtractMessage(JsonElement jsonElement)
        {
            if (jsonElement.TryGetProperty("mensagem", out var msgProp))
                return msgProp.GetString() ?? string.Empty;

            if (jsonElement.TryGetProperty("message", out var messageProp))
                return messageProp.GetString() ?? string.Empty;

            return string.Empty;
        }
        // Classe auxiliar para roteamento
        private class RoteamentoInfo
        {
            public bool Valido { get; set; }
            public string Servico { get; set; } = string.Empty;
            public string Endpoint { get; set; } = string.Empty;
            public string NomeCliente { get; set; } = string.Empty;
        }
    }
}

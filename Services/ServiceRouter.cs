using GatewayAPI.Interfaces;
using GatewayAPI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GatewayAPI.Services
{
    public class ServiceRouter : IServiceRouter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRouteRegistry _routeRegistry;
        private readonly ILogger<ServiceRouter> _logger;
        private readonly ITrackingService _trackingService;
        private readonly JsonSerializerOptions _jsonOptions;

        public ServiceRouter(
            IHttpClientFactory httpClientFactory,
            IRouteRegistry routeRegistry,
            ILogger<ServiceRouter> logger,
            ITrackingService trackingService)
        {
            _httpClientFactory = httpClientFactory;
            _routeRegistry = routeRegistry;
            _logger = logger;
            _trackingService = trackingService;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<GatewayResponse> RouteRequestAsync(GatewayRequest request)
        {
            // ⭐⭐ VALIDAÇÃO CRÍTICA: Verificar se o request é nulo
            if (request == null)
            {
                _logger.LogError("❌ Request é nulo");
                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 400,
                    ErrorMessage = "Request inválido: objeto nulo"
                };
            }

            // ⭐⭐ VALIDAÇÃO: Verificar propriedades essenciais
            if (string.IsNullOrWhiteSpace(request.Service))
            {
                _logger.LogError("❌ Service é nulo ou vazio");
                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 400,
                    ErrorMessage = "Service é obrigatório"
                };
            }

            // ⭐⭐ CORREÇÃO: Tratar Endpoint nulo
            string endpoint = request.Endpoint ?? string.Empty;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                _logger.LogWarning("⚠️ Endpoint está vazio, usando valor padrão 'api/default'");
                endpoint = "api/default";
            }

            ProdutoTracking tracking = null;
            string tipoServico = "desconhecido";

            try
            {
                _logger.LogInformation("🔀 Roteando: {Service}/{Endpoint}", request.Service, endpoint);

                // DETECTAR TIPO DE SERVIÇO
                bool isProdutoService = request.Service.Equals("ProgramaProdutos", StringComparison.OrdinalIgnoreCase) ||
                                       endpoint.Contains("produtos", StringComparison.OrdinalIgnoreCase);

                bool isFornecedorService = request.Service.Equals("ProgramaFornecedores", StringComparison.OrdinalIgnoreCase) ||
                                          endpoint.Contains("fornecedores", StringComparison.OrdinalIgnoreCase);

                if (isProdutoService) tipoServico = "produto";
                if (isFornecedorService) tipoServico = "fornecedor";

                _logger.LogDebug("📊 Tipo de serviço detectado: {TipoServico}", tipoServico);

                // REGISTRAR TRACKING
                if (request.Data != null && (isProdutoService || isFornecedorService))
                {
                    try
                    {
                        var (codigoTarefa, descricao) = ExtrairDadosTracking(request.Data, tipoServico);

                        if (!string.IsNullOrEmpty(codigoTarefa))
                        {
                            tracking = _trackingService.RegistrarEnvio(codigoTarefa, request.Data);
                            tracking.TipoServico = tipoServico;
                            tracking.Descricao = descricao;

                            _logger.LogInformation("📝 Tracking iniciado: {CodigoTarefa} ({Tipo})",
                                codigoTarefa, tipoServico.ToUpper());

                            request.Headers ??= new Dictionary<string, string>();
                            request.Headers["X-Tracking-Id"] = tracking.Id;
                            request.Headers["X-Tracking-Tipo"] = tipoServico;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Não foi possível registrar tracking");
                    }
                }

                // ENCONTRAR SERVIÇO
                var serviceConfig = _routeRegistry.GetService(request.Service);

                // FALLBACK PARA FORNECEDORES NÃO CONFIGURADOS
                if (serviceConfig == null && isFornecedorService)
                {
                    _logger.LogWarning("⚠️ Serviço de fornecedores não encontrado, tentando ProgramaFornecedores...");
                    serviceConfig = _routeRegistry.GetService("ProgramaFornecedores");
                }

                if (serviceConfig == null)
                {
                    _logger.LogWarning("⚠️ Serviço não encontrado: {Service}", request.Service);

                    if (tracking != null)
                    {
                        _trackingService.RegistrarErro(tracking.CodigoTarefa,
                            $"Serviço '{request.Service}' não encontrado", tipoServico);
                    }

                    return new GatewayResponse
                    {
                        Success = false,
                        StatusCode = 404,
                        ErrorMessage = $"Serviço '{request.Service}' não encontrado"
                    };
                }

                _logger.LogDebug("📋 Config do serviço: BaseUrl={BaseUrl}", serviceConfig.BaseUrl);

                // ⭐⭐ CORREÇÃO: Tratar PathTemplate nulo
                string pathTemplate = serviceConfig.PathTemplate ?? "/{endpoint}";

                // CONSTRUIR URL
                var baseUrl = serviceConfig.BaseUrl?.TrimEnd('/') ?? "";
                endpoint = endpoint.TrimStart('/');

                // ⭐⭐ CORREÇÃO: Substituir endpoint no template de forma segura
                var path = pathTemplate.Replace("{endpoint}", endpoint, StringComparison.OrdinalIgnoreCase);

                if (!path.StartsWith("/")) path = "/" + path;
                var fullUrl = $"{baseUrl}{path}";

                _logger.LogInformation("🌐 Enviando para: {FullUrl} ({Tipo})", fullUrl, tipoServico);

                if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri))
                {
                    _logger.LogError("❌ URL inválida: {FullUrl}", fullUrl);

                    if (tracking != null)
                    {
                        _trackingService.RegistrarErro(tracking.CodigoTarefa, $"URL inválida: {fullUrl}", tipoServico);
                    }

                    return new GatewayResponse
                    {
                        Success = false,
                        StatusCode = 400,
                        ErrorMessage = $"URL inválida: {fullUrl}"
                    };
                }

                // CRIAR REQUEST HTTP
                var httpMethod = ParseHttpMethod(request.HttpMethod);
                using var httpRequest = new HttpRequestMessage
                {
                    Method = httpMethod,
                    RequestUri = uri
                };

                // ADICIONAR QUERY PARAMS (com tratamento de nulo)
                if (request.QueryParams != null && request.QueryParams.Any())
                {
                    var queryString = string.Join("&", request.QueryParams
                        .Where(kv => !string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value))
                        .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

                    if (!string.IsNullOrEmpty(queryString))
                    {
                        var uriBuilder = new UriBuilder(uri) { Query = queryString };
                        httpRequest.RequestUri = uriBuilder.Uri;
                    }
                }

                // ADICIONAR HEADERS (com tratamento de nulo)
                if (request.Headers != null)
                {
                    foreach (var header in request.Headers)
                    {
                        if (!string.IsNullOrEmpty(header.Key) && !string.IsNullOrEmpty(header.Value))
                        {
                            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }

                // ADICIONAR HEADERS DE TRACKING
                if (tracking != null)
                {
                    httpRequest.Headers.TryAddWithoutValidation("X-Tracking-Id", tracking.Id);
                    httpRequest.Headers.TryAddWithoutValidation("X-Tracking-Tipo", tracking.TipoServico);
                    httpRequest.Headers.TryAddWithoutValidation("X-Tracking-CodigoTarefa", tracking.CodigoTarefa);
                }

                // ADICIONAR CORPO DA REQUISIÇÃO
                if ((httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Patch)
                    && request.Data != null)
                {
                    var jsonContent = JsonSerializer.Serialize(request.Data, _jsonOptions);
                    httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    _logger.LogDebug("📦 Corpo da requisição: {JsonContent}", jsonContent);
                }

                // EXECUTAR REQUISIÇÃO
                using var httpClient = _httpClientFactory.CreateClient("GatewayClient");
                _logger.LogDebug("🚀 Enviando requisição: {Method} {Uri}", httpRequest.Method, httpRequest.RequestUri);

                var httpResponse = await httpClient.SendAsync(httpRequest);

                // PROCESSAR RESPOSTA
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogDebug("📨 Resposta recebida: {StatusCode} - {Length} bytes",
                    httpResponse.StatusCode, responseContent.Length);

                // ATUALIZAR TRACKING
                if (tracking != null)
                {
                    try
                    {
                        if (httpResponse.IsSuccessStatusCode)
                        {
                            var respostaObj = JsonSerializer.Deserialize<object>(responseContent, _jsonOptions);
                            _trackingService.AtualizarResposta(tracking.CodigoTarefa, respostaObj, tracking.TipoServico);
                            _logger.LogInformation("✅ Tracking atualizado: {CodigoTarefa} ({Tipo})",
                                tracking.CodigoTarefa, tracking.TipoServico);
                        }
                        else
                        {
                            _trackingService.RegistrarErro(tracking.CodigoTarefa,
                                $"HTTP {httpResponse.StatusCode}: {responseContent}", tracking.TipoServico);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Não foi possível atualizar tracking");
                    }
                }

                // DESSERIALIZAR RESPOSTA
                object responseData;
                try
                {
                    if (!string.IsNullOrEmpty(responseContent) &&
                        (responseContent.TrimStart().StartsWith("{") || responseContent.TrimStart().StartsWith("[")))
                    {
                        responseData = JsonSerializer.Deserialize<object>(responseContent, _jsonOptions) ?? responseContent;
                    }
                    else
                    {
                        responseData = responseContent;
                    }
                }
                catch (JsonException)
                {
                    responseData = responseContent;
                }

                // ADICIONAR TRACKING À RESPOSTA
                if (tracking != null)
                {
                    try
                    {
                        if (responseData is JsonElement jsonElement)
                        {
                            responseData = AdicionarTrackingAoJson(jsonElement, tracking);
                        }
                        else if (responseData is string responseString && responseString.TrimStart().StartsWith("{"))
                        {
                            try
                            {
                                jsonElement = JsonSerializer.Deserialize<JsonElement>(responseString, _jsonOptions);
                                responseData = AdicionarTrackingAoJson(jsonElement, tracking);
                            }
                            catch
                            {
                                // Mantém string original
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Não foi possível adicionar tracking à resposta");
                    }
                }

                // HEADERS DA RESPOSTA
                var headersDict = httpResponse.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
                if (tracking != null)
                {
                    headersDict["X-Tracking-Id"] = tracking.Id;
                    headersDict["X-Tracking-CodigoTarefa"] = tracking.CodigoTarefa;
                    headersDict["X-Tracking-Tipo"] = tracking.TipoServico;
                    headersDict["X-Tracking-Status"] = tracking.Status;
                }

                return new GatewayResponse
                {
                    Success = httpResponse.IsSuccessStatusCode,
                    StatusCode = (int)httpResponse.StatusCode,
                    Data = responseData,
                    Headers = headersDict,
                    ErrorMessage = httpResponse.IsSuccessStatusCode ? null : $"Erro {httpResponse.StatusCode}: {responseContent}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao rotear requisição para {Service}/{Endpoint}",
                    request.Service, endpoint);

                if (tracking != null)
                {
                    _trackingService.RegistrarErro(tracking.CodigoTarefa, $"Exceção: {ex.Message}", tipoServico);
                }

                return new GatewayResponse
                {
                    Success = false,
                    StatusCode = 500,
                    ErrorMessage = $"Erro interno: {ex.Message}",
                    Headers = tracking != null
                        ? new Dictionary<string, string>
                        {
                            ["X-Tracking-Id"] = tracking.Id,
                            ["X-Tracking-CodigoTarefa"] = tracking.CodigoTarefa,
                            ["X-Tracking-Tipo"] = tipoServico
                        }
                        : null
                };
            }
        }

        // ⭐ NOVO: Método para extrair dados de tracking baseado no tipo
        private (string CodigoTarefa, string Descricao) ExtrairDadosTracking(object data, string tipoServico)
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(data, _jsonOptions);
                var jsonDoc = JsonDocument.Parse(jsonString);
                var root = jsonDoc.RootElement;

                string codigoTarefa = string.Empty;
                string descricao = string.Empty;

                // Tenta obter codigoTarefa da raiz
                if (root.TryGetProperty("codigoTarefa", out var codigoProp) &&
                    codigoProp.ValueKind == JsonValueKind.String)
                {
                    codigoTarefa = codigoProp.GetString() ?? string.Empty;
                }

                // Tenta obter descrição baseado no tipo
                if (tipoServico == "produto")
                {
                    if (root.TryGetProperty("dados", out var dadosProp))
                    {
                        var dadosJson = dadosProp.GetRawText();
                        var dadosDoc = JsonDocument.Parse(dadosJson);
                        var dadosRoot = dadosDoc.RootElement;

                        if (dadosRoot.TryGetProperty("descricao", out var descricaoProp) &&
                            descricaoProp.ValueKind == JsonValueKind.String)
                        {
                            descricao = descricaoProp.GetString() ?? string.Empty;
                        }
                    }
                }
                else if (tipoServico == "fornecedor")
                {
                    if (root.TryGetProperty("dados", out var dadosProp))
                    {
                        var dadosJson = dadosProp.GetRawText();
                        var dadosDoc = JsonDocument.Parse(dadosJson);
                        var dadosRoot = dadosDoc.RootElement;

                        // ⭐⭐ CORREÇÃO: razaoSocial pode ser nulo, então verificar primeiro CNPJ
                        if (dadosRoot.TryGetProperty("cnpj", out var cnpjProp) &&
                            cnpjProp.ValueKind == JsonValueKind.String)
                        {
                            var cnpj = cnpjProp.GetString() ?? string.Empty;
                            descricao = $"CNPJ: {cnpj}";

                            // Se tiver razaoSocial, adiciona
                            if (dadosRoot.TryGetProperty("razaoSocial", out var razaoProp) &&
                                razaoProp.ValueKind == JsonValueKind.String)
                            {
                                var razao = razaoProp.GetString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(razao))
                                {
                                    descricao = $"{razao} ({cnpj})";
                                }
                            }
                        }
                        // ⭐ NOVO: Se não tem CNPJ mas tem razaoSocial
                        else if (dadosRoot.TryGetProperty("razaoSocial", out var razaoProp) &&
                                 razaoProp.ValueKind == JsonValueKind.String)
                        {
                            descricao = razaoProp.GetString() ?? "Fornecedor";
                        }
                    }
                }

                // Fallback para requestId (formato antigo)
                if (string.IsNullOrEmpty(codigoTarefa))
                {
                    if (root.TryGetProperty("requestId", out var requestIdProp) &&
                        requestIdProp.ValueKind == JsonValueKind.String)
                    {
                        codigoTarefa = requestIdProp.GetString() ?? string.Empty;
                    }
                }

                return (codigoTarefa, descricao);
            }
            catch
            {
                return (string.Empty, string.Empty);
            }
        }

        // ⭐ NOVO: Método para adicionar tracking ao JSON
        private Dictionary<string, object> AdicionarTrackingAoJson(JsonElement jsonElement, ProdutoTracking tracking)
        {
            var responseDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                jsonElement.GetRawText(), _jsonOptions) ?? new Dictionary<string, object>();

            // Adicionar informações de tracking
            responseDict["tracking"] = new
            {
                id = tracking.Id,
                codigoTarefa = tracking.CodigoTarefa,
                tipoServico = tracking.TipoServico,
                status = tracking.Status,
                dataEnvio = tracking.DataEnvio,
                dataResposta = tracking.DataResposta,
                descricao = tracking.Descricao
            };

            responseDict["gatewayProcessado"] = true;
            responseDict["gatewayTimestamp"] = DateTime.UtcNow.ToString("o");
            responseDict["tipoServico"] = tracking.TipoServico;

            return responseDict;
        }

        // Método para converter string para HttpMethod
        private HttpMethod ParseHttpMethod(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
                return HttpMethod.Post;

            return method.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                "HEAD" => HttpMethod.Head,
                "OPTIONS" => HttpMethod.Options,
                "TRACE" => HttpMethod.Trace,
                _ => HttpMethod.Post
            };
        }

        // ⭐ NOVO: Método especializado para produtos
        public async Task<GatewayResponse> RouteToProdutosAsync(string endpoint, object? data = null)
        {
            var request = new GatewayRequest
            {
                Service = "ProgramaProdutos",
                Endpoint = endpoint,
                HttpMethod = "POST",
                Data = data
            };

            return await RouteRequestAsync(request);
        }

        // ⭐ NOVO: Método especializado para fornecedores
        public async Task<GatewayResponse> RouteToFornecedoresAsync(string endpoint, object? data = null)
        {
            var request = new GatewayRequest
            {
                Service = "ProgramaFornecedores",
                Endpoint = endpoint,
                HttpMethod = "POST",
                Data = data
            };

            return await RouteRequestAsync(request);
        }

        public async Task<GatewayResponse> RouteRequestAsync(string service, string endpoint, HttpMethod method, object? data = null)
        {
            var request = new GatewayRequest
            {
                Service = service,
                Endpoint = endpoint,
                HttpMethod = method.Method,
                Data = data
            };

            return await RouteRequestAsync(request);
        }

        private string GenerateCacheKey(GatewayRequest request)
        {
            var keyParts = new List<string>
            {
                request.Service,
                request.Endpoint,
                request.HttpMethod ?? "POST"
            };

            if (request.QueryParams != null && request.QueryParams.Any())
            {
                keyParts.Add(string.Join("&", request.QueryParams
                    .OrderBy(kv => kv.Key)
                    .Select(kv => $"{kv.Key}={kv.Value}")));
            }

            return string.Join(":", keyParts);
        }
    }
}
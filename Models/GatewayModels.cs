using System.Text.Json.Serialization;

namespace GatewayAPI.Models
{
    /// <summary>
    /// Modelo unificado para qualquer tipo de tarefa
    /// </summary>
    public class GatewayTarefaRequest
    {
        [JsonPropertyName("codigoTarefa")]
        public string CodigoTarefa { get; set; } = string.Empty;

        [JsonPropertyName("tipoTarefa")] 
        public string TipoTarefa { get; set; } = string.Empty; 

        [JsonPropertyName("dados")]
        public Dictionary<string, object> Dados { get; set; } = new Dictionary<string, object>();

    }

    /// <summary>
    /// Resposta padrão do Gateway
    /// </summary>
    public class GatewayTarefaResponse
    {
        [JsonPropertyName("sucesso")]
        public bool Sucesso { get; set; }

        [JsonPropertyName("codigoTarefa")]
        public string CodigoTarefa { get; set; } = string.Empty;

        [JsonPropertyName("tipoTarefa")]
        public string TipoTarefa { get; set; } = string.Empty;

        [JsonPropertyName("codigoGerado")]
        public string CodigoGerado { get; set; } = string.Empty; 

        [JsonPropertyName("mensagem")]
        public string Mensagem { get; set; } = string.Empty;

        [JsonPropertyName("dataProcessamento")]
        public DateTime DataProcessamento { get; set; }

        [JsonPropertyName("servicoDestino")]
        public string ServicoDestino { get; set; } = string.Empty;

        [JsonPropertyName("gatewayProcessado")]
        public bool GatewayProcessado { get; set; } = true;
    }
    public class GatewayDatasheetRequest
    {
        [JsonPropertyName("codigoTarefa")]
        public string CodigoTarefa { get; set; } = string.Empty;

        [JsonPropertyName("dados")]
        public DatasheetDados Dados { get; set; } = new DatasheetDados();
    }

    public class DatasheetDados
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

    }
}
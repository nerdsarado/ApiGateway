using System.Text.Json.Serialization;

namespace GatewayAPI.Models
{
    public class ProdutoTracking
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CodigoTarefa { get; set; } = string.Empty;
        public string CodigoProduto { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string TipoServico { get; set; } = "produto";
        public decimal Custo { get; set; }
        public string NCM { get; set; } = string.Empty;
        public DateTime DataEnvio { get; set; } = DateTime.UtcNow;
        public DateTime? DataResposta { get; set; }
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public string Status { get; set; } = "Pendente";
        public int Tentativas { get; set; }
        public string ErroDetalhado { get; set; } = string.Empty;
        public Dictionary<string, object> Metadados { get; set; } = new();
    }

    public class TrackingResponse
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public ProdutoTracking Tracking { get; set; } = new();
        public List<ProdutoTracking> Historico { get; set; } = new();
    }
    public class GatewayClienteRequest
    {
        [JsonPropertyName("codigoTarefa")]
        public string CodigoTarefa { get; set; } = string.Empty;

        [JsonPropertyName("dados")]
        public ClienteDados Dados { get; set; } = new ClienteDados();
    }
    public class ClienteDados
    {
        [JsonPropertyName("cnpj")]
        public string CNPJ { get; set; } = string.Empty;

        [JsonPropertyName("inscricaoEstadual")]
        public string InscricaoEstadual { get; set; } = string.Empty; // Opcional
    }
}
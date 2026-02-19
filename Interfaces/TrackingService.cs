using GatewayAPI.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.Json;

namespace GatewayAPI.Services
{
    public interface ITrackingService
    {
        ProdutoTracking RegistrarEnvio(string codigoTarefa, object dados);
        void AtualizarResposta(string codigoTarefa, object resposta, string tipoServico = "produto");
        void RegistrarErro(string codigoTarefa, string erro, string tipoServico = "produto");
        ProdutoTracking ConsultarTracking(string codigoTarefa);
        IEnumerable<ProdutoTracking> GetAllTrackings();
    }

    public class TrackingService : ITrackingService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<TrackingService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private const string CACHE_KEY_TRACKINGS = "ProdutoTrackings";

        public TrackingService(IMemoryCache cache, ILogger<TrackingService> logger)
        {
            _cache = cache;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        public ProdutoTracking RegistrarEnvio(string codigoTarefa, object dados)
        {
            // Implementação...
            var tracking = new ProdutoTracking
            {
                Id = Guid.NewGuid().ToString(),
                CodigoTarefa = codigoTarefa,
                Status = "Processando",
                DataEnvio = DateTime.UtcNow
            };

            var trackings = ObterTrackings();
            trackings[tracking.CodigoTarefa] = tracking;
            SalvarTrackings(trackings);

            return tracking;
        }

        // ⭐⭐ IMPLEMENTAÇÃO CORRETA - com parâmetro tipoServico
        public void AtualizarResposta(string codigoTarefa, object resposta, string tipoServico = "produto")
        {
            var trackings = ObterTrackings();
            if (trackings.TryGetValue(codigoTarefa, out var tracking))
            {
                tracking.Status = "Sucesso";
                tracking.DataResposta = DateTime.UtcNow;;
                tracking.TipoServico = tipoServico;
                SalvarTrackings(trackings);
            }
        }

        // ⭐⭐ IMPLEMENTAÇÃO CORRETA - com parâmetro tipoServico
        public void RegistrarErro(string codigoTarefa, string erro, string tipoServico = "produto")
        {
            var trackings = ObterTrackings();
            if (trackings.TryGetValue(codigoTarefa, out var tracking))
            {
                tracking.Status = "Erro";
                tracking.DataResposta = DateTime.UtcNow;
                tracking.TipoServico = tipoServico;
                SalvarTrackings(trackings);
            }
        }

        public ProdutoTracking ConsultarTracking(string codigoTarefa)
        {
            var trackings = ObterTrackings();
            if (trackings.TryGetValue(codigoTarefa, out var tracking))
            {
                return tracking;
            }
            return new ProdutoTracking { Status = "Não encontrado" };
        }

        public IEnumerable<ProdutoTracking> GetAllTrackings()
        {
            var trackings = ObterTrackings();
            return trackings.Values;
        }

        private Dictionary<string, ProdutoTracking> ObterTrackings()
        {
            if (!_cache.TryGetValue(CACHE_KEY_TRACKINGS, out Dictionary<string, ProdutoTracking> trackings))
            {
                trackings = new Dictionary<string, ProdutoTracking>();
                _cache.Set(CACHE_KEY_TRACKINGS, trackings);
            }
            return trackings;
        }

        private void SalvarTrackings(Dictionary<string, ProdutoTracking> trackings)
        {
            _cache.Set(CACHE_KEY_TRACKINGS, trackings);
        }
    }
}
using GatewayAPI.Interfaces;
using GatewayAPI.Models;
using GatewayAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace GatewayAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackingController : ControllerBase
    {
        private readonly ITrackingService _trackingService;
        private readonly ILogger<TrackingController> _logger;

        public TrackingController(ITrackingService trackingService, ILogger<TrackingController> logger)
        {
            _trackingService = trackingService;
            _logger = logger;
        }

        [HttpGet("{codigoTarefa}")]
        public IActionResult GetTracking(string codigoTarefa)
        {
            try
            {
                _logger.LogInformation("🔍 Consultando tracking: {CodigoTarefa}", codigoTarefa);

                // ⭐ CORREÇÃO: Use ConsultarTracking, não ObterTracking
                var tracking = _trackingService.ConsultarTracking(codigoTarefa);

                if (tracking == null || tracking.Status == "Não encontrado")
                {
                    return NotFound(new
                    {
                        sucesso = false,
                        mensagem = $"Tracking não encontrado para: {codigoTarefa}"
                    });
                }

                return Ok(new
                {
                    sucesso = true,
                    tracking = tracking
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao consultar tracking: {CodigoTarefa}", codigoTarefa);
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = $"Erro interno: {ex.Message}"
                });
            }
        }

        [HttpGet]
        public IActionResult GetAllTrackings()
        {
            try
            {
                _logger.LogInformation("📋 Listando todos os trackings");

                // ⭐ CORREÇÃO: Use GetAllTrackings, não ObterTodosTrackings
                var trackings = _trackingService.GetAllTrackings();

                return Ok(new
                {
                    sucesso = true,
                    total = trackings.Count(),
                    trackings = trackings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao listar trackings");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = $"Erro interno: {ex.Message}"
                });
            }
        }

        [HttpGet("dashboard")]
        public IActionResult GetDashboard()
        {
            try
            {
                _logger.LogInformation("📊 Gerando dashboard de tracking");

                var trackings = _trackingService.GetAllTrackings().ToList();

                var dashboard = new
                {
                    sucesso = true,
                    dashboard = new
                    {
                        total = trackings.Count,
                        sucesso = trackings.Count(t => t.Status == "Sucesso"),
                        erros = trackings.Count(t => t.Status == "Erro"),
                        processando = trackings.Count(t => t.Status == "Processando"),
                        hoje = trackings.Count(t => t.DataEnvio.Date == DateTime.UtcNow.Date)
                    }
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao gerar dashboard");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = $"Erro interno: {ex.Message}"
                });
            }
        }

        [HttpGet("tipo/{tipoServico}")]
        public IActionResult GetTrackingsPorTipo(string tipoServico)
        {
            try
            {
                _logger.LogInformation("📊 Listando trackings por tipo: {TipoServico}", tipoServico);

                // Se sua interface tiver este método (opcional)
                // var trackings = _trackingService.GetTrackingsPorTipo(tipoServico);

                // ⭐ SOLUÇÃO: Filtre manualmente se o método não existir
                var todosTrackings = _trackingService.GetAllTrackings();
                var trackingsFiltrados = todosTrackings
                    .Where(t => t.TipoServico?.Equals(tipoServico, StringComparison.OrdinalIgnoreCase) ?? false)
                    .ToList();

                return Ok(new
                {
                    sucesso = true,
                    tipoServico = tipoServico,
                    total = trackingsFiltrados.Count,
                    trackings = trackingsFiltrados
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao listar trackings por tipo");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = $"Erro interno: {ex.Message}"
                });
            }
        }
    }
}
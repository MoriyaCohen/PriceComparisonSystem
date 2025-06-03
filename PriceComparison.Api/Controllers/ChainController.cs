using Microsoft.AspNetCore.Mvc;
using PriceComparison.Infrastructure.Repositories;

namespace PriceComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChainsController : ControllerBase
    {
        private readonly IChainRepository _chainRepository;
        private readonly ILogger<ChainsController> _logger;

        public ChainsController(IChainRepository chainRepository, ILogger<ChainsController> logger)
        {
            _chainRepository = chainRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                _logger.LogInformation("Fetching all chains");
                var chains = await _chainRepository.GetAllAsync();
                return Ok(chains);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching chains");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest("ID must be positive");

                var chain = await _chainRepository.GetByIdAsync(id);
                if (chain == null)
                    return NotFound($"Chain with ID {id} not found");

                return Ok(chain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching chain {ChainId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("by-chain-id/{chainId}")]
        public async Task<IActionResult> GetByChainId(string chainId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(chainId))
                    return BadRequest("Chain ID is required");

                var chain = await _chainRepository.GetByChainIdAsync(chainId);
                if (chain == null)
                    return NotFound($"Chain with Chain ID {chainId} not found");

                return Ok(chain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching chain {ChainId}", chainId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
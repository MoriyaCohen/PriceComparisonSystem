using Microsoft.AspNetCore.Mvc;
using PriceComparison.Infrastructure.Repositories;

namespace PriceComparison.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IProductRepository productRepository, ILogger<ProductsController> logger)
        {
            _productRepository = productRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                _logger.LogInformation("Fetching all products");
                var products = await _productRepository.GetAllAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching products");
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

                var product = await _productRepository.GetByIdAsync(id);
                if (product == null)
                    return NotFound($"Product with ID {id} not found");

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching product {ProductId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("by-barcode/{barcode}")]
        public async Task<IActionResult> GetByBarcode(string barcode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(barcode))
                    return BadRequest("Barcode is required");

                var product = await _productRepository.GetByBarcodeAsync(barcode);
                if (product == null)
                    return NotFound($"Product with barcode {barcode} not found");

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching product by barcode {Barcode}", barcode);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("by-category/{categoryId}")]
        public async Task<IActionResult> GetByCategory(int categoryId)
        {
            try
            {
                if (categoryId <= 0)
                    return BadRequest("Category ID must be positive");

                var products = await _productRepository.GetByCategoryAsync(categoryId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching products by category {CategoryId}", categoryId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search/{productName}")]
        public async Task<IActionResult> SearchByName(string productName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(productName))
                    return BadRequest("Product name is required");

                var products = await _productRepository.SearchByNameAsync(productName);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching products by name {ProductName}", productName);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
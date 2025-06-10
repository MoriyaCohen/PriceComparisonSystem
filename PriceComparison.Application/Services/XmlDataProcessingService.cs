using Microsoft.Extensions.Logging;
using PriceComparison.Application.DTOs;
using PriceComparison.Application.Services;
using PriceComparison.Infrastructure.Repositories;
using PriceComparison.Infrastructure.Models;

namespace PriceComparison.Application.Services
{
    public class XmlProcessingService : IXmlProcessingService
    {
        private readonly IChainRepository _chainRepository;
        private readonly IProductRepository _productRepository;
        private readonly IStoreRepository _storeRepository;
        private readonly IStorePriceRepository _storePriceRepository;
        private readonly ILogger<XmlProcessingService> _logger;

        public XmlProcessingService(
            IChainRepository chainRepository,
            IProductRepository productRepository,
            IStoreRepository storeRepository,
            IStorePriceRepository storePriceRepository,
            ILogger<XmlProcessingService> logger)
        {
            _chainRepository = chainRepository;
            _productRepository = productRepository;
            _storeRepository = storeRepository;
            _storePriceRepository = storePriceRepository;
            _logger = logger;
        }

        public async Task<ProcessingResult> ProcessXmlDataAsync(XmlUploadRequest request)
        {
            var result = new ProcessingResult();

            try
            {
                _logger.LogInformation("Starting XML processing for chain {ChainId}, store {StoreId}",
                    request.StoreInfo.ChainId, request.StoreInfo.StoreId);

                // 1. וידוא שהרשת קיימת או יצירתה
                var chain = await EnsureChainExistsAsync(request.StoreInfo);
                _logger.LogInformation("Chain processed: {ChainName}", chain.ChainName);

                // 2. וידוא שהסניף קיים או יצירתו
                var store = await EnsureStoreExistsAsync(request.StoreInfo, chain.Id);
                _logger.LogInformation("Store processed: {StoreName}", store.StoreName);

                // 3. עיבוד המוצרים
                var (newItems, updatedItems) = await ProcessProductsAsync(request.Items, store.Id);

                result.Success = true;
                result.ProcessedItems = request.Items.Count;
                result.NewItems = newItems;
                result.UpdatedItems = updatedItems;
                result.StoreInfo = request.StoreInfo;

                _logger.LogInformation("Processing completed successfully: {NewItems} new, {UpdatedItems} updated",
                    newItems, updatedItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing XML data");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<bool> ValidateXmlDataAsync(XmlUploadRequest request)
        {
            try
            {
                // בדיקות בסיסיות
                if (string.IsNullOrEmpty(request.StoreInfo.ChainId))
                    return false;

                if (string.IsNullOrEmpty(request.StoreInfo.StoreId))
                    return false;

                if (request.Items == null || !request.Items.Any())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<Chain> EnsureChainExistsAsync(StoreInfoDto storeInfo)
        {
            var existingChain = await _chainRepository.GetByChainIdAsync(storeInfo.ChainId);
            if (existingChain != null)
                return existingChain;

            // יצירת רשת חדשה אם לא קיימת
            var newChain = new Chain
            {
                ChainId = storeInfo.ChainId,
                ChainName = string.IsNullOrEmpty(storeInfo.ChainName) ? $"רשת {storeInfo.ChainId}" : storeInfo.ChainName,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            return await _chainRepository.AddAsync(newChain);
        }

        private async Task<Store> EnsureStoreExistsAsync(StoreInfoDto storeInfo, int chainId)
        {
            var existingStore = await _storeRepository.GetByStoreIdAsync(chainId, storeInfo.StoreId);
            if (existingStore != null)
            {
                // עדכון פרטי הסניף אם השתנו
                existingStore.BikoretNo = storeInfo.BikoretNo;
                existingStore.SubChainId = storeInfo.SubChainId;
                return await _storeRepository.UpdateAsync(existingStore);
            }

            var newStore = new Store
            {
                ChainId = chainId,
                StoreId = storeInfo.StoreId,
                StoreName = string.IsNullOrEmpty(storeInfo.StoreName) ? $"סניף {storeInfo.StoreId}" : storeInfo.StoreName,
                SubChainId = storeInfo.SubChainId,
                BikoretNo = storeInfo.BikoretNo,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            return await _storeRepository.AddAsync(newStore);
        }

        private async Task<(int newItems, int updatedItems)> ProcessProductsAsync(List<ProductItemDto> items, int storeId)
        {
            int newItems = 0, updatedItems = 0;

            foreach (var item in items)
            {
                try
                {
                    // 1. וידוא שהמוצר קיים או יצירתו
                    var product = await EnsureProductExistsAsync(item);

                    // 2. בדיקה האם יש מחיר קיים לחנות זו
                    var existingPrice = await _storePriceRepository.GetByStoreAndProductAsync(storeId, product.Id);

                    if (existingPrice == null)
                    {
                        // יצירת מחיר חדש
                        await _storePriceRepository.AddAsync(new StorePrice
                        {
                            StoreId = storeId,
                            ProductId = product.Id,
                            ItemCode = item.ItemCode,
                            CurrentPrice = item.ItemPrice,
                            UnitPrice = item.UnitOfMeasurePrice,
                            StockQuantity = item.QtyInPackage.ToString(),
                            ItemStatus = item.ItemStatus,
                            AllowDiscount = item.AllowDiscount,
                            FirstSeen = DateTime.Now,
                            LastUpdated = DateTime.Now
                        });
                        newItems++;
                    }
                    else if (existingPrice.CurrentPrice != item.ItemPrice ||
                             existingPrice.UnitPrice != item.UnitOfMeasurePrice ||
                             existingPrice.ItemStatus != item.ItemStatus ||
                             existingPrice.AllowDiscount != item.AllowDiscount)
                    {
                        // עדכון מחיר קיים
                        existingPrice.CurrentPrice = item.ItemPrice;
                        existingPrice.UnitPrice = item.UnitOfMeasurePrice;
                        existingPrice.ItemStatus = item.ItemStatus;
                        existingPrice.AllowDiscount = item.AllowDiscount;
                        existingPrice.StockQuantity = item.QtyInPackage.ToString();
                        existingPrice.LastUpdated = DateTime.Now;

                        await _storePriceRepository.UpdateAsync(existingPrice);
                        updatedItems++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing item {ItemCode}", item.ItemCode);
                    // ממשיך לפריט הבא
                }
            }

            return (newItems, updatedItems);
        }

        private async Task<Product> EnsureProductExistsAsync(ProductItemDto item)
        {
            // חיפוש לפי קוד פריט תחילה
            var existingProduct = await _productRepository.GetByProductIdAsync(item.ItemCode);
            if (existingProduct != null)
            {
                // עדכון פרטי המוצר אם השתנו
                existingProduct.ProductName = item.ItemName;
                existingProduct.ManufacturerName = item.ManufacturerName;
                existingProduct.UnitOfMeasure = item.UnitOfMeasure;
                existingProduct.IsWeighted = item.IsWeighted;
                existingProduct.QtyInPackage = item.QtyInPackage;

                return await _productRepository.UpdateAsync(existingProduct);
            }

            // יצירת מוצר חדש - כלל פשוט: ItemCode = Barcode תמיד
            string barcode = item.ItemCode;  // תמיד ברקוד
            int? categoryId = null;          // אף פעם לא קטגוריה

            _logger.LogInformation("Setting ItemCode as barcode: {Barcode}", barcode);

            var newProduct = new Product
            {
                ProductId = item.ItemCode,
                ProductName = item.ItemName,
                ManufacturerName = item.ManufacturerName,
                UnitOfMeasure = item.UnitOfMeasure,
                IsWeighted = item.IsWeighted,
                QtyInPackage = item.QtyInPackage,
                Barcode = barcode,           // תמיד יהיה ערך
                CategoryId = null,           // תמיד null
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            return await _productRepository.AddAsync(newProduct);
        }
    }
}
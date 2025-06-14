using PriceComparison.Application.DTOs;

namespace PriceComparison.Application.Services
{
    public interface IXmlProcessingService
    {
        Task<ProcessingResult> ProcessXmlDataAsync(XmlUploadRequest request);
        Task<bool> ValidateXmlDataAsync(XmlUploadRequest request);
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAKControllers.DataAccess;
using System.ComponentModel.DataAnnotations;

namespace RAKControllers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SampleDistributionController : ControllerBase
    {
        private readonly DatabaseHelper _dbHelper;

        public SampleDistributionController(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [HttpPost("register")]
        public IActionResult RegisterSampleDistribution([FromBody] SampleDistributionRequest request = null)
        {
            try
            {
                // Handle null request gracefully
                if (request == null)
                {
                    request = new SampleDistributionRequest();
                }

                // Validate material quantity
                if (request.MaterialQuantity <= 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Material quantity must be greater than 0",
                        timestamp = DateTime.Now
                    });
                }

                // Parse distribution date safely
                DateTime? distributionDateParsed = SafeParseDate(request.DistributionDate);

                // Insert sample distribution registration
                var insertQuery = @"
                    INSERT INTO SampleDistributions
                    (emiratesId, area, retailerName, retailerCode, concernDistributor,
                     painterName, painterMobile, skuSize, materialQuantity, distributionDate,
                     createdt, isactive)
                    VALUES
                    (@emiratesId, @area, @retailerName, @retailerCode, @concernDistributor,
                     @painterName, @painterMobile, @skuSize, @materialQuantity, @distributionDate,
                     GETDATE(), 'Y');
                    SELECT SCOPE_IDENTITY();";

                var parameters = new Dictionary<string, object>
                {
                    // Retailer Details
                    { "@emiratesId", SafeString(request.EmiratesId) },
                    { "@area", SafeString(request.Area) },
                    { "@retailerName", SafeString(request.RetailerName) },
                    { "@retailerCode", SafeString(request.RetailerCode) },
                    { "@concernDistributor", SafeString(request.ConcernDistributor) },

                    // Distribution Details
                    { "@painterName", SafeString(request.PainterName) },
                    { "@painterMobile", SafeString(request.PainterMobile) },
                    { "@skuSize", SafeString(request.SkuSize) },
                    { "@materialQuantity", request.MaterialQuantity },
                    { "@distributionDate", distributionDateParsed ?? (object)DBNull.Value }
                };

                var result = _dbHelper.WebSessBean(insertQuery, parameters);

                var distributionId = result?.FirstOrDefault()?.Values?.FirstOrDefault()?.ToString() ?? "0";

                return Ok(new
                {
                    success = true,
                    message = "Sample distribution registered successfully",
                    distributionId = distributionId,
                    distributionDetails = new
                    {
                        materialQuantity = $"{request.MaterialQuantity} Kg",
                        skuSize = request.SkuSize
                    },
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                // Even if database fails, return success with error details
                return Ok(new
                {
                    success = false,
                    message = "Registration completed with warnings",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        

        [HttpGet("areas")]
        public IActionResult GetAreas()
        {
            var areas = new[]
            {
                "Area 1",
                "Area 2",
                "Area 3"
            };

            return Ok(new
            {
                success = true,
                data = areas,
                timestamp = DateTime.Now
            });
        }

        [HttpGet("sku-sizes")]
        public IActionResult GetSkuSizes()
        {
            var skuSizes = new[]
            {
                "1 Kg",
                "5 Kg"
            };

            return Ok(new
            {
                success = true,
                data = skuSizes,
                timestamp = DateTime.Now
            });
        }

        [HttpGet("distribution-summary")]
        public IActionResult GetDistributionSummary([FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
        {
            try
            {
                var dateCondition = "";
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                {
                    dateCondition = "AND distributionDate BETWEEN @StartDate AND @EndDate";
                    parameters.Add("@StartDate", startDate);
                    parameters.Add("@EndDate", endDate);
                }

                var query = $@"
                    SELECT
                        COUNT(*) as TotalDistributions,
                        SUM(materialQuantity) as TotalQuantity,
                        AVG(materialQuantity) as AverageQuantity,
                        COUNT(DISTINCT retailerCode) as UniqueRetailers,
                        COUNT(DISTINCT painterName) as UniquePainters
                    FROM SampleDistributions
                    WHERE isactive = 'Y' {dateCondition}";

                var result = _dbHelper.WebSessBean(query, parameters);

                if (result == null || result.Count == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            totalDistributions = 0,
                            totalQuantity = 0.0,
                            averageQuantity = 0.0,
                            uniqueRetailers = 0,
                            uniquePainters = 0
                        },
                        timestamp = DateTime.Now
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = result[0],
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Failed to get distribution summary",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        // Helper methods for safe data handling
        private static string SafeString(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? "" : input.Trim();
        }

        private static DateTime? SafeParseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return null;

            // Try multiple date formats
            var formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy/MM/dd", "MM-dd-yyyy", "dd-MM-yyyy" };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(dateString, format, null, System.Globalization.DateTimeStyles.None, out DateTime result))
                {
                    return result;
                }
            }

            // Fallback to general parsing
            if (DateTime.TryParse(dateString, out DateTime generalResult))
            {
                return generalResult;
            }

            return null;
        }
    }

    // Request Models
    public class SampleDistributionRequest
    {
        // Retailer Details
        public string EmiratesId { get; set; } = "";
        public string Area { get; set; } = "";
        public string RetailerName { get; set; } = "";
        public string RetailerCode { get; set; } = "";
        public string ConcernDistributor { get; set; } = "";

        // Distribution Details
        public string PainterName { get; set; } = "";
        public string PainterMobile { get; set; } = "";
        public string SkuSize { get; set; } = "";
        public decimal MaterialQuantity { get; set; } = 0;
        public string DistributionDate { get; set; } = "";
    }

    public class SampleDistributionUpdateRequest
    {
        // Retailer Details
        public string EmiratesId { get; set; } = "";
        public string Area { get; set; } = "";
        public string RetailerName { get; set; } = "";
        public string RetailerCode { get; set; } = "";
        public string ConcernDistributor { get; set; } = "";

        // Distribution Details
        public string PainterName { get; set; } = "";
        public string PainterMobile { get; set; } = "";
        public string SkuSize { get; set; } = "";
        public decimal MaterialQuantity { get; set; } = 0;
        public string DistributionDate { get; set; } = "";
    }
}
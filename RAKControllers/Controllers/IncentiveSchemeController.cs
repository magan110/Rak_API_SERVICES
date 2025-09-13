using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAKControllers.DataAccess;
using System.ComponentModel.DataAnnotations;

namespace RAKControllers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IncentiveSchemeController : ControllerBase
    {
        private readonly DatabaseHelper _dbHelper;

        public IncentiveSchemeController(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [HttpPost("register")]
        public IActionResult RegisterIncentiveScheme([FromBody] IncentiveSchemeRequest request = null)
        {
            try
            {
                // Handle null request gracefully
                if (request == null)
                {
                    request = new IncentiveSchemeRequest();
                }

                // Validate quantity limits
                if (request.MaterialQuantity < 1 || request.MaterialQuantity > 1000)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Material quantity must be between 1 and 1000 bags",
                        timestamp = DateTime.Now
                    });
                }

                // Calculate benefit per bag based on role
                var benefitPerBag = GetBenefitPerBag(request.BeneficiaryRole);
                var totalBenefit = request.MaterialQuantity * benefitPerBag;

                // Parse month safely
                DateTime? schemeMonthParsed = SafeParseMonth(request.SchemeMonth);

                // Insert incentive scheme registration
                var insertQuery = @"
                    INSERT INTO IncentiveSchemes
                    (beneficiaryRole, beneficiaryName, contactNumber, channelPartnerName, invoiceNumber,
                     schemeMonth, materialQuantity, benefitPerBag, totalBenefit, remarks,
                     createdt, isactive)
                    VALUES
                    (@beneficiaryRole, @beneficiaryName, @contactNumber, @channelPartnerName, @invoiceNumber,
                     @schemeMonth, @materialQuantity, @benefitPerBag, @totalBenefit, @remarks,
                     GETDATE(), 'Y');
                    SELECT SCOPE_IDENTITY();";

                var parameters = new Dictionary<string, object>
                {
                    { "@beneficiaryRole", SafeString(request.BeneficiaryRole) },
                    { "@beneficiaryName", SafeString(request.BeneficiaryName) },
                    { "@contactNumber", SafeString(request.ContactNumber) },
                    { "@channelPartnerName", SafeStringOrNull(request.ChannelPartnerName) },
                    { "@invoiceNumber", SafeString(request.InvoiceNumber) },
                    { "@schemeMonth", schemeMonthParsed ?? (object)DBNull.Value },
                    { "@materialQuantity", request.MaterialQuantity },
                    { "@benefitPerBag", benefitPerBag },
                    { "@totalBenefit", totalBenefit },
                    { "@remarks", SafeStringOrNull(request.Remarks) }
                };

                var result = _dbHelper.WebSessBean(insertQuery, parameters);

                var schemeId = result?.FirstOrDefault()?.Values?.FirstOrDefault()?.ToString() ?? "0";

                // Check for high quantity alert
                var highQuantityAlert = request.MaterialQuantity > 500;

                return Ok(new
                {
                    success = true,
                    message = "Incentive scheme registered successfully",
                    schemeId = schemeId,
                    benefitDetails = new
                    {
                        benefitPerBag = $"{benefitPerBag} AED/bag",
                        totalBenefit = $"{totalBenefit} AED",
                        materialQuantity = request.MaterialQuantity
                    },
                    alerts = highQuantityAlert ? new[] { "High quantity entry detected (>500 bags). Please verify the entry." } : new string[0],
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

        

        [HttpGet("beneficiary-roles")]
        public IActionResult GetBeneficiaryRoles()
        {
            var roles = new[]
            {
                "Retailer",
                "Purchase Manager",
                "Salesman",
                "Contractor/Painter"
            };

            return Ok(new
            {
                success = true,
                data = roles,
                timestamp = DateTime.Now
            });
        }

        [HttpGet("benefit-calculator/{role}/{quantity}")]
        public IActionResult CalculateBenefit(string role, int quantity)
        {
            try
            {
                if (quantity < 1 || quantity > 1000)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Quantity must be between 1 and 1000 bags",
                        timestamp = DateTime.Now
                    });
                }

                var benefitPerBag = GetBenefitPerBag(role);
                var totalBenefit = quantity * benefitPerBag;
                var highQuantityAlert = quantity > 500;

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        role = role,
                        quantity = quantity,
                        benefitPerBag = $"{benefitPerBag} AED/bag",
                        totalBenefit = $"{totalBenefit} AED"
                    },
                    alerts = highQuantityAlert ? new[] { "High quantity entry detected (>500 bags). Please verify the entry." } : new string[0],
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    message = "Failed to calculate benefit",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        // Helper method to get benefit per bag based on role
        private decimal GetBenefitPerBag(string role)
        {
            var benefitMap = new Dictionary<string, decimal>
            {
                { "Retailer", 1.0m },
                { "Purchase Manager", 0.5m },
                { "Salesman", 1.0m },
                { "Contractor/Painter", 1.0m }
            };

            return benefitMap.ContainsKey(role) ? benefitMap[role] : 0.0m;
        }

        // Helper method to parse month string (MMMM yyyy format)
        private DateTime? SafeParseMonth(string monthString)
        {
            if (string.IsNullOrWhiteSpace(monthString))
                return null;

            try
            {
                // Try parsing "MMMM yyyy" format (e.g., "January 2024")
                var formats = new[] { "MMMM yyyy", "MMM yyyy", "MM/yyyy", "M/yyyy" };
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(monthString, format, null, System.Globalization.DateTimeStyles.None, out DateTime result))
                    {
                        return result;
                    }
                }

                // Fallback to general parsing
                if (DateTime.TryParse(monthString, out DateTime generalResult))
                {
                    return generalResult;
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }

        // Helper methods for safe data handling
        private static string SafeString(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? "" : input.Trim();
        }

        private static object SafeStringOrNull(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? (object)DBNull.Value : input.Trim();
        }
    }

    // Request Models
    public class IncentiveSchemeRequest
    {
        // Beneficiary Type
        public string BeneficiaryRole { get; set; } = "";

        // Exchange Details
        public string BeneficiaryName { get; set; } = "";
        public string ContactNumber { get; set; } = "";
        public string ChannelPartnerName { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public string SchemeMonth { get; set; } = "";
        public int MaterialQuantity { get; set; } = 0;
        public string Remarks { get; set; } = "";
    }

    public class IncentiveSchemeUpdateRequest
    {
        // Beneficiary Type
        public string BeneficiaryRole { get; set; } = "";

        // Exchange Details
        public string BeneficiaryName { get; set; } = "";
        public string ContactNumber { get; set; } = "";
        public string ChannelPartnerName { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public string SchemeMonth { get; set; } = "";
        public int MaterialQuantity { get; set; } = 0;
        public string Remarks { get; set; } = "";
    }
}
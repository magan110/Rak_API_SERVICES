using Microsoft.AspNetCore.Mvc;
using RAKControllers.DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAKControllers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class RetailerOnboardingController : ControllerBase
    {
        private readonly DatabaseHelper _db;
        public RetailerOnboardingController(DatabaseHelper dbHelper) => _db = dbHelper;

        // POST: api/RetailerOnboarding/register
        [HttpPost("register")]
        [Consumes("application/json")]
        public IActionResult RegisterRetailer([FromBody] RetailerOnboardingRequest? request = null)
        {
            try
            {
                request ??= new RetailerOnboardingRequest();

                // Dates: keep NULL if not provided ("" is invalid for smalldatetime)
                DateTime? estDate = SafeParseDate(request.EstablishmentDate);
                DateTime? expDate = SafeParseDate(request.ExpiryDate);
                DateTime? effvDate = SafeParseDate(request.EffectiveRegistrationDate);

                // Generate retlCode (8 chars)
                string retlCode = GenerateRetailerCode();

                // Map Flutter → table; everything NOT from Flutter => "" (empty string)
                var p = new Dictionary<string, object?>
                {
                    // Required identity/audit
                    ["@retlCode"] = retlCode,
                    ["@sapRtlCd"] = "",       // not in Flutter → ""
                    ["@createId"] = "",       // not in Flutter → ""
                    ["@createDt"] = DateTime.Now, // audit we set server-side
                    ["@isActive"] = "Y",      // default active

                    // Names / Address (from Flutter or "")
                    ["@retlName"] = SafeStringOrEmpty(request.FirmName),
                    ["@retlChNm"] = SafeStringOrEmpty(request.FirmName),
                    ["@contName"] = SafeStringOrEmpty(request.ResponsiblePerson),
                    ["@location"] = SafeStringOrEmpty(request.RegisteredAddress),
                    ["@retlAdd1"] = SafeStringOrEmpty(request.RegisteredAddress),
                    ["@retlAdd2"] = "",
                    ["@retlAdd3"] = "",
                    ["@district"] = "",
                    ["@retlPinC"] = "",

                    // Codes / fixed fields (not in Flutter → "")
                    ["@custCode"] = "",
                    ["@areaCode"] = "",
                    ["@locStxNo"] = "",
                    ["@retlCatg"] = "",
                    ["@typeOfBs"] = "",
                    ["@busSttYr"] = "",
                    ["@busBwYrs"] = "",
                    ["@schmAbFL"] = "",
                    ["@ubsRtlF1"] = "",

                    // Phones / email (not in Flutter → "")
                    ["@offTelN1"] = "",
                    ["@offTelN2"] = "",
                    ["@mobileNo"] = "",
                    ["@emailAdd"] = "",

                    // KYC flags (not in Flutter → "")
                    ["@benfNmFl"] = "",
                    ["@itxPanNo"] = "",

                    // Bank (map what exists in Flutter; not present → "")
                    ["@bankAcNo"] = "",
                    ["@benfName"] = SafeStringOrEmpty(request.AccountHolderName),
                    ["@bankBnNm"] = SafeStringOrEmpty(request.BranchName),
                    ["@bankBnDs"] = SafeStringOrEmpty(request.BranchAddress),
                    ["@bankBnNo"] = "",
                    ["@bankIFSC"] = "",
                    ["@ibannmbr"] = SafeStringOrEmpty(request.IbanNumber), // added column

                    // VAT / License (map; dates stay NULL if not supplied)
                    ["@gstnNumb"] = SafeStringOrEmpty(request.TaxRegistrationNumber),
                    ["@licnumbr"] = SafeStringOrEmpty(request.LicenseNumber),
                    ["@issauthr"] = SafeStringOrEmpty(request.IssuingAuthority),
                    ["@tradname"] = SafeStringOrEmpty(request.TradeName),
                    ["@respprsn"] = SafeStringOrEmpty(request.ResponsiblePerson),
                    ["@estdatee"] = (object?)estDate ?? DBNull.Value,
                    ["@licexpdt"] = (object?)expDate ?? DBNull.Value,
                    ["@effregdt"] = (object?)effvDate ?? DBNull.Value,

                    // Location
                    ["@latitute"] = SafeStringOrEmpty(request.Latitude),
                    ["@lgtitute"] = SafeStringOrEmpty(request.Longitude),
                };

                var sql = @"
INSERT INTO dbo.rtmRetailer
(
    retlCode, sapRtlCd, retlName, retlChNm, contName, custCode, areaCode, location,
    retlAdd1, retlAdd2, retlAdd3, district, retlPinC,
    benfNmFl, bankAcNo, benfName, bankBnNm, bankBnDs, bankBnNo, bankIFSC,
    retlCatg, itxPanNo, offTelN1, offTelN2, mobileNo, emailAdd, typeOfBs, busSttYr, busBwYrs,
    isActive, createId, createDt, locStxNo, schmAbFL, ubsRtlF1,
    gstnNumb, licnumbr, issauthr, estdatee, licexpdt, tradname, respprsn, effregdt,
    ibannmbr, latitute, lgtitute
)
VALUES
(
    @retlCode, @sapRtlCd, @retlName, @retlChNm, @contName, @custCode, @areaCode, @location,
    @retlAdd1, @retlAdd2, @retlAdd3, @district, @retlPinC,
    @benfNmFl, @bankAcNo, @benfName, @bankBnNm, @bankBnDs, @bankBnNo, @bankIFSC,
    @retlCatg, @itxPanNo, @offTelN1, @offTelN2, @mobileNo, @emailAdd, @typeOfBs, @busSttYr, @busBwYrs,
    @isActive, @createId, @createDt, @locStxNo, @schmAbFL, @ubsRtlF1,
    @gstnNumb, @licnumbr, @issauthr, @estdatee, @licexpdt, @tradname, @respprsn, @effregdt,
    @ibannmbr, @latitute, @lgtitute
);

SELECT @retlCode AS RetailerCode;
";

                var res = _db.WebSessBean(sql, p) ?? new List<Dictionary<string, object>>();
                var code = res.FirstOrDefault()?["RetailerCode"]?.ToString() ?? retlCode;

                return Ok(new
                {
                    success = true,
                    message = "Retailer onboarded successfully",
                    retailerCode = code,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Onboarding failed",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        // -------- helpers --------
        private static string SafeStringOrEmpty(string? s)
            => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

        private static DateTime? SafeParseDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString)) return null;
            var fmts = new[] { "yyyy-MM-dd","MM/dd/yyyy","dd/MM/yyyy","yyyy/MM/dd",
                               "MM-dd-yyyy","dd-MM-yyyy","yyyyMMdd","ddMMyyyy" };
            foreach (var f in fmts)
                if (DateTime.TryParseExact(dateString, f, null,
                    System.Globalization.DateTimeStyles.None, out var dt)) return dt;
            if (DateTime.TryParse(dateString, out var any)) return any;
            return null;
        }

        private static string GenerateRetailerCode()
        {
            var n = (int)(DateTime.UtcNow - new DateTime(2020, 1, 1)).TotalSeconds % 9999999;
            return "R" + n.ToString().PadLeft(7, '0'); // 8 chars
        }
    }

    // DTO: only fields that exist in the Flutter code
    public class RetailerOnboardingRequest
    {
        // VAT
        public string? FirmName { get; set; }
        public string? TaxRegistrationNumber { get; set; }
        public string? RegisteredAddress { get; set; }
        public string? EffectiveRegistrationDate { get; set; }

        // License
        public string? LicenseNumber { get; set; }
        public string? IssuingAuthority { get; set; }
        public string? EstablishmentDate { get; set; }
        public string? ExpiryDate { get; set; }
        public string? TradeName { get; set; }
        public string? ResponsiblePerson { get; set; }

        // Bank
        public string? AccountHolderName { get; set; }
        public string? IbanNumber { get; set; }
        public string? BankName { get; set; }     // no column; ignored/empty
        public string? BranchName { get; set; }
        public string? BranchAddress { get; set; }

        // Location
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
    }
}

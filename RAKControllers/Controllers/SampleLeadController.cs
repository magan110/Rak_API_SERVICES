using Microsoft.AspNetCore.Mvc;

using RAKControllers.DataAccess;

using System;

using System.Collections.Generic;

using System.Data;

using System.Linq;

namespace RAKControllers.Controllers

{

    [Route("api/[controller]")]

    [ApiController]

    [Produces("application/json")]

    public class SampleLeadController : ControllerBase

    {

        private readonly DatabaseHelper _dbHelper;

        public SampleLeadController(DatabaseHelper dbHelper)

        {

            _dbHelper = dbHelper;

        }

        // POST: api/SampleLead/register

        [HttpPost("register")]

        [Consumes("application/json")]

        public IActionResult RegisterSampleLead([FromBody] SampleLeadRequest? request = null)

        {

            try

            {

                request ??= new SampleLeadRequest();

                // Parse dates from strings

                DateTime? trgDate = SafeParseDate(request.TargetDateOfConversion);

                DateTime? sampDate = SafeParseDate(request.SamplingDate);

                // Generate a 16-char doc number: YYMMDD + 6 from GUID + 4 random

                var docuNumb = GenerateDocNumber();

                // Map Flutter site type text to table siteType (varchar(1))

                // e.g., "Type 1", "A", "Urban" -> take first character if present.

                var siteType1Char = (request.SiteType ?? "").Trim();

                siteType1Char = string.IsNullOrEmpty(siteType1Char) ? "" : siteType1Char.Substring(0, 1);

                // Insert. For cols present in table but not in Flutter code -> pass empty string (""),

                // and sensible defaults for dates (createDt GETDATE()).

                var insertSql = @"

                    INSERT INTO dbo.catsamplead

                    (

                        docuNumb, docuDate,

                        areaCode, district, sitePinC, custName, contName, mobileNo,

                        siteAddr, siteType, leadSorc, latitude, longtude, leadRmrk,

                        statFlag, atchNmId, createId, createDt, updateId, updataeDt,

                        pendWith, appAuths, apprDate, apprFlag, pasngDat, apprveBy,

                        bilPasBy, bilPasHR, billHrdt, blHRmnth, stgCntrc, trgtDate,

                        urbRurFl,

                        sampRcvr, cnvTrgDt, consRegn, sampDate, prodName, smExOrdr, sampType

                    )

                    VALUES

                    (

                        @docuNumb, GETDATE(),

                        @areaCode, @district, @sitePinC, @custName, @contName, @mobileNo,

                        @siteAddr, @siteType, @leadSorc, @latitude, @longtude, @leadRmrk,

                        @statFlag, @atchNmId, @createId, GETDATE(), @updateId, @updataeDt,

                        @pendWith, @appAuths, @apprDate, @apprFlag, @pasngDat, @apprveBy,

                        @bilPasBy, @bilPasHR, @billHrdt, @blHRmnth, @stgCntrc, @trgtDate,

                        @urbRurFl,

                        @sampRcvr, @cnvTrgDt, @consRegn, @sampDate, @prodName, @smExOrdr, @sampType

                    );";

                var p = new Dictionary<string, object?>

                {

                    // Keys

                    ["@docuNumb"] = docuNumb,

                    // From Flutter (map to table)

                    ["@areaCode"] = SafeStr(request.Area),

                    ["@district"] = SafeStr(request.CityDistrict),

                    ["@sitePinC"] = SafeStr(request.PinCode),

                    ["@custName"] = SafeStr(request.CustomerName),

                    ["@contName"] = SafeStr(request.ContractorName),

                    ["@mobileNo"] = SafeStr(request.MobileNumber),

                    ["@siteAddr"] = SafeStr(request.Address),

                    ["@siteType"] = SafeStr(siteType1Char),  // only 1 char

                    ["@latitude"] = SafeStr(request.Latitude),

                    ["@longtude"] = SafeStr(request.Longitude),

                    ["@leadRmrk"] = SafeStr(request.Remarks),

                    // New/added fields

                    ["@sampRcvr"] = SafeStr(request.SampleLocalReceivedPerson),

                    ["@cnvTrgDt"] = (object?)trgDate ?? DBNull.Value,     // added col (Target date of conversion)

                    ["@consRegn"] = SafeStr(request.RegionOfConstruction),

                    ["@sampDate"] = (object?)sampDate ?? DBNull.Value,     // added col

                    ["@prodName"] = SafeStr(request.Product),

                    ["@smExOrdr"] = SafeStr(request.SiteMaterialExpectedOrder),

                    ["@sampType"] = SafeStr(request.SampleType),

                    // Also set built-in trgtDate with same TargetDateOfConversion

                    ["@trgtDate"] = (object?)trgDate ?? DBNull.Value,

                    // Columns present in table but NOT coming from Flutter -> pass empty string or NULL date

                    ["@leadSorc"] = "",     // no field in flutter

                    ["@statFlag"] = "",     // no field in flutter

                    ["@atchNmId"] = "",     // upload not required

                    ["@createId"] = "",     // can fill user id later

                    ["@updateId"] = "",

                    ["@updataeDt"] = DBNull.Value,

                    ["@pendWith"] = "",

                    ["@appAuths"] = "",

                    ["@apprDate"] = DBNull.Value,

                    ["@apprFlag"] = "",

                    ["@pasngDat"] = DBNull.Value,

                    ["@apprveBy"] = "",

                    ["@bilPasBy"] = "",

                    ["@bilPasHR"] = "",

                    ["@billHrdt"] = DBNull.Value,

                    ["@blHRmnth"] = "",

                    ["@stgCntrc"] = "",

                    ["@urbRurFl"] = ""      // could derive from Region if you want

                };

                _dbHelper.WebExecute(insertSql, p);

                return Ok(new

                {

                    success = true,

                    message = "Sample lead captured successfully.",

                    documentNumber = docuNumb,

                    timestamp = DateTime.Now

                });

            }

            catch (Exception ex)

            {

                return BadRequest(new

                {

                    success = false,

                    message = "Failed to capture sample lead.",

                    error = ex.Message,

                    timestamp = DateTime.Now

                });

            }

        }

        // ----------------- helpers -----------------

        private static string GenerateDocNumber()

        {

            // 16 chars: YYMMDD + 6 GUID + 4 random digits

            var ymd = DateTime.Now.ToString("yyMMdd");

            var mid = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();

            var rnd = new Random().Next(1000, 9999).ToString();

            var doc = (ymd + mid + rnd);

            return doc.Length <= 16 ? doc : doc.Substring(0, 16);

        }

        private static object? SafeStr(string? s)

            => string.IsNullOrWhiteSpace(s) ? (object?)"" : s.Trim();

        private static DateTime? SafeParseDate(string? dateString)

        {

            if (string.IsNullOrWhiteSpace(dateString)) return null;

            var formats = new[]

            {

                "yyyy-MM-dd","MM/dd/yyyy","dd/MM/yyyy","yyyy/MM/dd",

                "MM-dd-yyyy","dd-MM-yyyy","yyyyMMdd","ddMMyyyy"

            };

            foreach (var f in formats)

            {

                if (DateTime.TryParseExact(dateString, f, null,

                    System.Globalization.DateTimeStyles.None, out var dt))

                {

                    return dt;

                }

            }

            if (DateTime.TryParse(dateString, out var any)) return any;

            return null;

        }

    }

    // ----------------- request model -----------------

    public class SampleLeadRequest

    {

        // Flutter fields

        public string? Area { get; set; }                           // -> areaCode

        public string? CityDistrict { get; set; }                   // -> district

        public string? PinCode { get; set; }                        // -> sitePinC

        public string? CustomerName { get; set; }                   // -> custName

        public string? ContractorName { get; set; }                 // -> contName

        public string? MobileNumber { get; set; }                   // -> mobileNo

        public string? Address { get; set; }                        // -> siteAddr

        public string? SiteType { get; set; }                       // -> siteType (1 char)

        public string? SampleLocalReceivedPerson { get; set; }      // -> sampRcvr (added)

        public string? TargetDateOfConversion { get; set; }         // -> cnvTrgDt (added) + trgtDate

        public string? Remarks { get; set; }                        // -> leadRmrk

        public string? RegionOfConstruction { get; set; }           // -> consRegn (added)

        public string? Latitude { get; set; }                       // -> latitude

        public string? Longitude { get; set; }                      // -> longtude

        public string? SamplingDate { get; set; }                   // -> sampDate (added)

        public string? Product { get; set; }                        // -> prodName (added)

        public string? SiteMaterialExpectedOrder { get; set; }      // -> smExOrdr (added)

        public string? SampleType { get; set; }                     // -> sampType (added)

    }

}


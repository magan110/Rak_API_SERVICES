using Microsoft.AspNetCore.Mvc;
using RAKControllers.DataAccess;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RAKControllers.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class PainterController : ControllerBase
    {
        private readonly DatabaseHelper _db;

        public PainterController(DatabaseHelper dbHelper)
        {
            _db = dbHelper;
        }

        // POST: /api/Painter/register
        [HttpPost("register")]
        [Consumes("application/json")]
        public IActionResult Register([FromBody] PainterRegistrationRequest? request = null)
        {
            try
            {
                request ??= new PainterRegistrationRequest();

                // --- Keys / defaults ---
                var inflCode = MakeFixed(8, Guid.NewGuid().ToString("N").ToUpperInvariant()); // char(8)
                var inflType = "PN";                                                           // char(2) -> Painter
                var areaCode = (Left(request.Area, 3) ?? "").ToUpperInvariant();               // char(3)

                // Names
                var fullName = string.Join(" ",
                    new[] { request.FirstName, request.MiddleName, request.LastName }
                    .Where(v => !string.IsNullOrWhiteSpace(v))).Trim();

                var inflName256 = Cut(fullName, 256);  // inflName nvarchar(256)
                var contName40  = Cut(fullName, 40);   // contName varchar(40)

                // Address cluster (legacy)
                var inflAdd1_50   = Cut(request.Address, 50);
                var inflAdd2_100  = "";
                var inflAdd3_30   = "";                // keep empty (NOT NULL but "" allowed)
                var inflCity_255  = "";

                // Mobile
                var mobile11 = FormatMobile11(request.MobileNumber); // varchar(11)

                // Bank (legacy)
                var bankName100   = Cut(request.BankName, 100);
                var branchName100 = Cut(request.BranchName, 100);
                var acctHolder40  = Cut(request.AccountHolderName, 40);

                // Painter extra (varchar 255 columns we added)
                var emiridno255 = Cut(request.EmiratesIdNumber, 255);
                var idholder255 = Cut(request.IdName, 255);
                var nationty255 = Cut(request.Nationality, 255);
                var employer255 = Cut(request.CompanyDetails, 255);
                var issudate255 = Cut(request.IssueDate, 255);
                var expirydt255 = Cut(request.ExpiryDate, 255);
                var occupatn255 = Cut(request.Occupation, 255);
                var bankaddr255 = Cut(request.BankAddress, 255);

                // Existing “new” 255 columns reused when relevant
                var contrtyp255 = "Painter";                            // painter role label
                var frstname255 = Cut(request.FirstName, 255);
                var middname255 = Cut(request.MiddleName, 255);
                var lastname255 = Cut(request.LastName, 255);
                var emirates255 = Cut(request.Emirates, 255);
                var referenc255 = Cut(request.Reference, 255);
                var ibannumb255 = Cut(FormatIban(request.IbanNumber), 255);

                // DOB -> map to legacy smalldatetime birthDat
                var birthDat = SafeParseDate(request.DateOfBirth);

                // Build parameter bag (EVERY param used in INSERT must exist)
                var p = new Dictionary<string, object?>
                {
                    // ==== Required legacy (NOT NULL) ====
                    ["@inflCode"] = inflCode,                // char(8)
                    ["@inflType"] = inflType,                // char(2)
                    ["@inflName"] = inflName256,             // nvarchar(256)
                    ["@contName"] = contName40,              // varchar(40)

                    // Address cluster
                    ["@inflAdd1"] = inflAdd1_50,             // varchar(50)
                    ["@inflAdd2"] = inflAdd2_100,            // varchar(100)
                    ["@inflAdd3"] = inflAdd3_30,             // varchar(30) NOT NULL
                    ["@inflCity"] = inflCity_255,            // varchar(255)
                    ["@district"] = "",                      // varchar(30)  NOT NULL
                    ["@inflPinC"] = "",                      // varchar(6)   NOT NULL

                    ["@mobileNo"] = mobile11,                // varchar(11)  NOT NULL
                    ["@emailAdd"] = "",                      // varchar(40)  NOT NULL
                    ["@busSttYr"] = "",                      // varchar(4)   NOT NULL
                    ["@birthDat"] = (object?)birthDat ?? DBNull.Value,
                    ["@inflQual"] = "",                      // varchar(20)  NOT NULL
                    ["@expertis"] = "",                      // varchar(100) YES (send "")
                    ["@wrkrDetl"] = "",                      // varchar(20)  NOT NULL
                    ["@anniDate"] = DBNull.Value,            // smalldatetime NULL
                    ["@landLnNo"] = "",                      // varchar(11)  NOT NULL
                    ["@inflLevl"] = "",                      // char(1)      NOT NULL
                    ["@isActive"] = "Y",                     // char(1)      NOT NULL
                    ["@createId"] = "API",                   // varchar(10)  NOT NULL
                    ["@updateId"] = DBNull.Value,
                    ["@updateDt"] = DBNull.Value,
                    ["@needMaru"] = "N",                     // varchar(1)   NOT NULL
                    ["@refDocNo"] = DBNull.Value,            // char(16)     NULL
                    ["@areaCode"] = areaCode,                // char(3)      NOT NULL

                    // Tax IDs / misc
                    ["@itxPanNo"] = "",
                    ["@locStxNo"] = "",

                    // Banking (legacy)
                    ["@bankAcNo"] = "",
                    ["@bankBnNm"] = bankName100,
                    ["@bankBnDs"] = branchName100,
                    ["@bankBnNo"] = "",
                    ["@bankIFSC"] = "",

                    // KYC & misc
                    ["@kycVerFl"] = "",
                    ["@kycVerDt"] = DBNull.Value,
                    ["@idCardTy"] = "",
                    ["@idCardNo"] = "",
                    ["@inDocCnt"] = DBNull.Value,
                    ["@rjcRemrk"] = "",
                    ["@entrBySg"] = "",
                    ["@kycVrTyp"] = "",
                    ["@adhrName"] = "",
                    ["@panCName"] = "",
                    ["@sapInsDt"] = DBNull.Value,
                    ["@sapUpdDt"] = DBNull.Value,
                    ["@urbRurFl"] = "",

                    ["@bankApNm"] = acctHolder40,            // varchar(40)

                    // Permanent/alt address
                    ["@infAddP1"] = "",
                    ["@infAddP2"] = "",
                    ["@infAddP3"] = "",
                    ["@infPCity"] = "",
                    ["@distrctP"] = "",
                    ["@infPinCP"] = "",

                    ["@adhOtpFl"] = "",
                    ["@concEmpl"] = "",
                    ["@bnkKycFl"] = "",
                    ["@bnkKycDt"] = DBNull.Value,
                    ["@inflPinL"] = "",
                    ["@adhCpUId"] = "",
                    ["@adhCpUDt"] = DBNull.Value,
                    ["@panCpUId"] = "",
                    ["@panCpUDt"] = DBNull.Value,
                    ["@bnkCpUId"] = "",
                    ["@bnkCpUDt"] = DBNull.Value,
                    ["@randNmSt"] = "",
                    ["@kycStats"] = "",
                    ["@inflTyOl"] = "",
                    ["@kycAprBy"] = "",
                    ["@blkStuts"] = "",
                    ["@blkStDat"] = DBNull.Value,
                    ["@cntrApFl"] = "",
                    ["@frstScDt"] = DBNull.Value,
                    ["@sourceCnt"] = "",
                    ["@promoteNm"] = "",
                    ["@pmobilen"] = "",
                    ["@isTDSEli"] = "",
                    ["@panAdhLk"] = "",
                    ["@panVldIn"] = "",
                    ["@panCatgF"] = "",

                    // ==== Existing 255s we reuse ====
                    ["@contrtyp"] = contrtyp255,
                    ["@frstname"] = frstname255,
                    ["@middname"] = middname255,
                    ["@lastname"] = lastname255,
                    ["@emirates"] = emirates255,
                    ["@referenc"] = referenc255,
                    ["@ibannumb"] = ibannumb255,
                    ["@vatfrmnm"] = "",         // not used for painter
                    ["@vatraddr"] = "",
                    ["@vattxreg"] = "",
                    ["@vatefdt"]  = "",
                    ["@comlcnno"] = "",
                    ["@comissau"] = "",
                    ["@comlctyp"] = "",
                    ["@comestdt"] = "",
                    ["@comexpdt"] = "",
                    ["@comtrdnm"] = "",
                    ["@comrespn"] = "",
                    ["@comraddr"] = "",
                    ["@comeffdt"] = "",

                    // ==== Painter-specific 8-char columns ====
                    ["@emiridno"] = emiridno255,   // Emirates ID Number
                    ["@idholder"] = idholder255,   // ID Holder Name
                    ["@nationty"] = nationty255,   // Nationality
                    ["@employer"] = employer255,   // Company / Employer
                    ["@issudate"] = issudate255,   // Issue Date (text)
                    ["@expirydt"] = expirydt255,   // Expiry Date (text)
                    ["@occupatn"] = occupatn255,   // Occupation
                    ["@bankaddr"] = bankaddr255,   // Bank Address
                };

                // INSERT (covers all required legacy + reused 255s + painter extras)
                var sql = @"
INSERT INTO dbo.ctmInfluncr
(
    inflCode, inflType, inflName, contName,
    inflAdd1, inflAdd2, inflAdd3, inflCity, district, inflPinC,
    mobileNo, emailAdd, busSttYr, birthDat, inflQual, expertis, wrkrDetl, anniDate, landLnNo,
    inflLevl, isActive, createId, createDt, updateId, updateDt, needMaru, refDocNo, areaCode,
    itxPanNo, locStxNo, bankAcNo, bankBnNm, bankBnDs, bankBnNo, bankIFSC,
    kycVerFl, kycVerDt, idCardTy, idCardNo, inDocCnt, rjcRemrk, entrBySg, kycVrTyp, adhrName,
    panCName, sapInsDt, sapUpdDt, urbRurFl, bankApNm,
    infAddP1, infAddP2, infAddP3, infPCity, distrctP, infPinCP,
    adhOtpFl, concEmpl, bnkKycFl, bnkKycDt, inflPinL,
    adhCpUId, adhCpUDt, panCpUId, panCpUDt, bnkCpUId, bnkCpUDt,
    randNmSt, kycStats, inflTyOl, kycAprBy, blkStuts, blkStDat, cntrApFl, frstScDt, sourceCnt,
    promoteNm, pmobilen, isTDSEli, panAdhLk, panVldIn, panCatgF,

    -- shared 255s
    contrtyp, frstname, middname, lastname, emirates, referenc, ibannumb,
    vatfrmnm, vatraddr, vattxreg, vatefdt,
    comlcnno, comissau, comlctyp, comestdt, comexpdt, comtrdnm, comrespn, comraddr, comeffdt,

    -- painter-specific 8-char name columns (varchar 255)
    emiridno, idholder, nationty, employer, issudate, expirydt, occupatn, bankaddr
)
VALUES
(
    @inflCode, @inflType, @inflName, @contName,
    @inflAdd1, @inflAdd2, @inflAdd3, @inflCity, @district, @inflPinC,
    @mobileNo, @emailAdd, @busSttYr, @birthDat, @inflQual, @expertis, @wrkrDetl, @anniDate, @landLnNo,
    @inflLevl, @isActive, @createId, GETDATE(), @updateId, @updateDt, @needMaru, @refDocNo, @areaCode,
    @itxPanNo, @locStxNo, @bankAcNo, @bankBnNm, @bankBnDs, @bankBnNo, @bankIFSC,
    @kycVerFl, @kycVerDt, @idCardTy, @idCardNo, @inDocCnt, @rjcRemrk, @entrBySg, @kycVrTyp, @adhrName,
    @panCName, @sapInsDt, @sapUpdDt, @urbRurFl, @bankApNm,
    @infAddP1, @infAddP2, @infAddP3, @infPCity, @distrctP, @infPinCP,
    @adhOtpFl, @concEmpl, @bnkKycFl, @bnkKycDt, @inflPinL,
    @adhCpUId, @adhCpUDt, @panCpUId, @panCpUDt, @bnkCpUId, @bnkCpUDt,
    @randNmSt, @kycStats, @inflTyOl, @kycAprBy, @blkStuts, @blkStDat, @cntrApFl, @frstScDt, @sourceCnt,
    @promoteNm, @pmobilen, @isTDSEli, @panAdhLk, @panVldIn, @panCatgF,

    @contrtyp, @frstname, @middname, @lastname, @emirates, @referenc, @ibannumb,
    @vatfrmnm, @vatraddr, @vattxreg, @vatefdt,
    @comlcnno, @comissau, @comlctyp, @comestdt, @comexpdt, @comtrdnm, @comrespn, @comraddr, @comeffdt,

    @emiridno, @idholder, @nationty, @employer, @issudate, @expirydt, @occupatn, @bankaddr
);

SELECT @inflCode AS InflCode;
";

                var result = _db.WebSessBean(sql, p) ?? new List<Dictionary<string, object>>();
                var savedCode = result.FirstOrDefault()?["InflCode"]?.ToString() ?? inflCode;

                return Ok(new
                {
                    success = true,
                    message = "Painter registration saved",
                    influencerCode = savedCode,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Registration failed",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        // (Optional) dropdowns if you need them for painter UI (copy from Contractor as needed)
        [HttpGet("emirates-list")]
        public IActionResult EmiratesList() => Ok(new
        {
            success = true,
            data = new[] { "Dubai","Abu Dhabi","Sharjah","Ajman","Umm Al Quwain","Ras Al Khaimah","Fujairah" },
            timestamp = DateTime.Now
        });

        // ----------------- helpers -----------------
        private static string MakeFixed(int size, string input)
        {
            if (string.IsNullOrEmpty(input)) input = "";
            return input.Length >= size ? input[..size]
                   : input + new string('A', size - input.Length);
        }

        private static string? Left(string? s, int n)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();
            return t.Length <= n ? t : t.Substring(0, n);
        }

        private static string Cut(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var t = s.Trim();
            return t.Length <= max ? t : t[..max];
        }

        private static string Digits(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return new string(s.Where(char.IsDigit).ToArray());
        }

        // keep last 11 digits if user typed country code
        private static string FormatMobile11(string? s)
        {
            var d = Digits(s);
            if (d.Length <= 11) return d;
            return d.Substring(d.Length - 11);
        }

        private static string? FormatIban(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            return s.Replace(" ", "").ToUpperInvariant();
        }

        private static DateTime? SafeParseDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString)) return null;
            var formats = new[] {
                "yyyy-MM-dd","MM/dd/yyyy","dd/MM/yyyy","yyyy/MM/dd",
                "MM-dd-yyyy","dd-MM-yyyy","yyyyMMdd","ddMMyyyy"
            };
            foreach (var f in formats)
            {
                if (DateTime.TryParseExact(dateString, f, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
                    return dt;
            }
            if (DateTime.TryParse(dateString, out var any)) return any;
            return null;
        }
    }

    // -------- request model (matches your Flutter painter screen) --------
    public class PainterRegistrationRequest
    {
        // Personal
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? MobileNumber { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? Emirates { get; set; }
        public string? Reference { get; set; }

        // Emirates ID Details
        public string? EmiratesIdNumber { get; set; }
        public string? IdName { get; set; }          // Name on ID
        public string? DateOfBirth { get; set; }     // maps to birthDat
        public string? Nationality { get; set; }
        public string? CompanyDetails { get; set; }  // Employer
        public string? IssueDate { get; set; }
        public string? ExpiryDate { get; set; }
        public string? Occupation { get; set; }

        // Bank Details (optional)
        public string? AccountHolderName { get; set; }
        public string? IbanNumber { get; set; }
        public string? BankName { get; set; }
        public string? BranchName { get; set; }
        public string? BankAddress { get; set; }
    }
}

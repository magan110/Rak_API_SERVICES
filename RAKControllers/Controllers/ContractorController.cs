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
    public class ContractorController : ControllerBase
    {
        private readonly DatabaseHelper _db;

        public ContractorController(DatabaseHelper dbHelper)
        {
            _db = dbHelper;
        }

        // POST: /api/Contractor/register
        [HttpPost("register")]
        [Consumes("application/json")]
        public IActionResult Register([FromBody] ContractorRegistrationRequest? request = null)
        {
            try
            {
                request ??= new ContractorRegistrationRequest();

                // --- Keys / defaults ---
                var inflCode = MakeFixed(8, Guid.NewGuid().ToString("N").ToUpperInvariant());   // char(8)
                var inflType = (Left(request.ContractorType, 2) ?? "").ToUpperInvariant();       // char(2)
                var areaCode = (Left(request.Area, 3) ?? "").ToUpperInvariant();                 // char(3)

                // Build names and enforce column sizes
                var fullName = string.Join(" ",
                    new[] { request.FirstName, request.MiddleName, request.LastName }
                    .Where(v => !string.IsNullOrWhiteSpace(v))).Trim();

                var inflName256 = Cut(fullName, 256); // inflName nvarchar(256)
                var contName40  = Cut(fullName, 40);  // contName varchar(40)

                // Address fields (NOT NULL: inflAdd3, district, inflPinC)
                var inflAdd1_50 = Cut(request.Address, 50);
                var inflAdd2_100 = "";            // not using; keep empty
                var inflAdd3_30 = "";             // keep empty to avoid overflow (NOT NULL but "" is allowed)
                var inflCity_255 = "";

                // Phone and other short fields
                var mobile11 = FormatMobile11(request.MobileNumber); // varchar(11)

                // Bank fields
                var bankName100   = Cut(request.BankName, 100);
                var branchName100 = Cut(request.BranchName, 100);
                var acctHolder40  = Cut(request.AccountHolderName, 40);

                // New 255 columns (cut to 255 just in case)
                var contrtyp255 = Cut(request.ContractorType, 255);
                var frstname255 = Cut(request.FirstName, 255);
                var middname255 = Cut(request.MiddleName, 255);
                var lastname255 = Cut(request.LastName, 255);
                var emirates255 = Cut(request.Emirates, 255);
                var referenc255 = Cut(request.Reference, 255);
                var ibannumb255 = Cut(FormatIban(request.IbanNumber), 255);
                var vatfrmnm255 = Cut(request.FirmName, 255);
                var vatraddr255 = Cut(request.VatAddress, 255);
                var vattxreg255 = Cut(request.TaxRegistrationNumber, 255);
                var vatefdt255  = Cut(request.VatEffectiveDate, 255);
                var comlcnno255 = Cut(request.LicenseNumber, 255);
                var comissau255 = Cut(request.IssuingAuthority, 255);
                var comlctyp255 = Cut(request.LicenseType, 255);
                var comestdt255 = Cut(request.EstablishmentDate, 255);
                var comexpdt255 = Cut(request.LicenseExpiryDate, 255);
                var comtrdnm255 = Cut(request.TradeName, 255);
                var comrespn255 = Cut(request.ResponsiblePerson, 255);
                var comraddr255 = Cut(request.LicenseAddress, 255);
                var comeffdt255 = Cut(request.EffectiveDate, 255);

                // Build parameter bag: supply EVERYTHING used in INSERT
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
                    ["@birthDat"] = DBNull.Value,            // smalldatetime NULL
                    ["@inflQual"] = "",                      // varchar(20)  NOT NULL
                    ["@expertis"] = "",                      // varchar(100) YES (send "")
                    ["@wrkrDetl"] = "",                      // varchar(20)  NOT NULL
                    ["@anniDate"] = DBNull.Value,            // smalldatetime NULL
                    ["@landLnNo"] = "",                      // varchar(11)  NOT NULL
                    ["@inflLevl"] = "",                      // char(1)      NOT NULL
                    ["@isActive"] = "Y",                     // char(1)      NOT NULL
                    ["@createId"] = "API",                   // varchar(10)  NOT NULL
                    ["@updateId"] = DBNull.Value,            // varchar(10)  NULL
                    ["@updateDt"] = DBNull.Value,            // smalldatetime NULL
                    ["@needMaru"] = "N",                     // varchar(1)   NOT NULL
                    ["@refDocNo"] = DBNull.Value,            // char(16)     NULL
                    ["@areaCode"] = areaCode,                // char(3)      NOT NULL

                    // Tax IDs / misc
                    ["@itxPanNo"] = "",                      // varchar(50)
                    ["@locStxNo"] = "",                      // varchar(30)

                    // Banking (legacy)
                    ["@bankAcNo"] = "",                      // varchar(25)
                    ["@bankBnNm"] = bankName100,             // varchar(100)
                    ["@bankBnDs"] = branchName100,           // varchar(100)
                    ["@bankBnNo"] = "",                      // varchar(20)
                    ["@bankIFSC"] = "",                      // varchar(20)

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

                    // ==== New varchar(255) columns ====
                    ["@contrtyp"] = contrtyp255,
                    ["@frstname"] = frstname255,
                    ["@middname"] = middname255,
                    ["@lastname"] = lastname255,
                    ["@emirates"] = emirates255,
                    ["@referenc"] = referenc255,
                    ["@ibannumb"] = ibannumb255,
                    ["@vatfrmnm"] = vatfrmnm255,
                    ["@vatraddr"] = vatraddr255,
                    ["@vattxreg"] = vattxreg255,
                    ["@vatefdt"]  = vatefdt255,
                    ["@comlcnno"] = comlcnno255,
                    ["@comissau"] = comissau255,
                    ["@comlctyp"] = comlctyp255,
                    ["@comestdt"] = comestdt255,
                    ["@comexpdt"] = comexpdt255,
                    ["@comtrdnm"] = comtrdnm255,
                    ["@comrespn"] = comrespn255,
                    ["@comraddr"] = comraddr255,
                    ["@comeffdt"] = comeffdt255
                };

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

    contrtyp, frstname, middname, lastname, emirates, referenc, ibannumb,
    vatfrmnm, vatraddr, vattxreg, vatefdt,
    comlcnno, comissau, comlctyp, comestdt, comexpdt, comtrdnm, comrespn, comraddr, comeffdt
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
    @comlcnno, @comissau, @comlctyp, @comestdt, @comexpdt, @comtrdnm, @comrespn, @comraddr, @comeffdt
);

SELECT @inflCode AS InflCode;
";

                var result = _db.WebSessBean(sql, p) ?? new List<Dictionary<string, object>>();
                var savedCode = result.FirstOrDefault()?["InflCode"]?.ToString() ?? inflCode;

                return Ok(new
                {
                    success = true,
                    message = "Registration saved",
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

        // Dropdowns to match Flutter UI
        [HttpGet("contractor-types")]
        public IActionResult ContractorTypes() => Ok(new
        {
            success = true,
            data = new[] { "Maintenance Contractor", "Petty contractors" },
            timestamp = DateTime.Now
        });

        [HttpGet("emirates-list")]
        public IActionResult EmiratesList() => Ok(new
        {
            success = true,
            data = new[] { "Dubai", "Abu Dhabi", "Sharjah", "Ajman", "Umm Al Quwain", "Ras Al Khaimah", "Fujairah" },
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
    }

    // ----------------- request model (matches Flutter) -----------------
    public class ContractorRegistrationRequest
    {
        // Personal
        public string? ContractorType { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? MobileNumber { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? Emirates { get; set; }
        public string? Reference { get; set; }
        public string? ProfilePhoto { get; set; }

        // Bank
        public string? AccountHolderName { get; set; }
        public string? IbanNumber { get; set; }
        public string? BankName { get; set; }
        public string? BranchName { get; set; }
        public string? BankAddress { get; set; }

        // VAT
        public string? VatCertificate { get; set; }
        public string? FirmName { get; set; }
        public string? VatAddress { get; set; }
        public string? TaxRegistrationNumber { get; set; }
        public string? VatEffectiveDate { get; set; }

        // License
        public string? LicenseDocument { get; set; }
        public string? LicenseNumber { get; set; }
        public string? IssuingAuthority { get; set; }
        public string? LicenseType { get; set; }
        public string? EstablishmentDate { get; set; }
        public string? LicenseExpiryDate { get; set; }
        public string? TradeName { get; set; }
        public string? ResponsiblePerson { get; set; }
        public string? LicenseAddress { get; set; }
        public string? EffectiveDate { get; set; }
    }
}

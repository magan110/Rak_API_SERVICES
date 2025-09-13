using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RAKControllers.DataAccess;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace sparshWebService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class AuthController : ControllerBase
    {
        private readonly DatabaseHelper _dbHelper;

        private const int ITERATION_NUMBER = 1;

        public AuthController(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [AllowAnonymous]
        [HttpPost("execute")]
        public IActionResult ExecuteStoredProcedure([FromBody] AuthRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.userID) || string.IsNullOrWhiteSpace(request.password))
            {
                return BadRequest("Parameter 'userID' and 'password' is required.");
            }

            try
            {
                var query = "SELECT loginIdm, hashPass, hashSalt FROM wcmUserCred WHERE loginIdM = @userID";
                var parameters = new Dictionary<string, object> { { "@userID", request.userID } };

                var result = _dbHelper.WebSessBean(query, parameters);

                if (result == null || result.Count == 0)
                {
               //     Console.WriteLine("❌ No user found with userID: " + request.userID);
                    return Unauthorized("Invalid userID.");
                }

              //  Console.WriteLine("✅ Retrieved user record:");
                foreach (var key in result[0].Keys)
                {
                   // Console.WriteLine($"  {key} → {result[0][key]}");
                }

                string storedHash = result[0]["hashPass"]?.ToString();
                string storedSalt = result[0]["hashSalt"]?.ToString();

                if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
                {
                //    Console.WriteLine("❌ Stored hash or salt is missing or empty.");
                    return BadRequest("Hash or salt not configured for this user.");
                }

                string inputHash = HashPassword(request.password, storedSalt);

               /* Console.WriteLine("🔐 Comparing hashes:");
                Console.WriteLine("→ Input Password: " + request.password);
                Console.WriteLine("→ Stored Salt   : " + storedSalt);
                Console.WriteLine("→ Stored Hash   : " + storedHash);
                Console.WriteLine("→ Computed Hash : " + inputHash);
               */
                if (!string.Equals(inputHash, storedHash, StringComparison.Ordinal))
                {
                 //   Console.WriteLine("❌ Hash mismatch.");
                    return Unauthorized("Invalid password.");
                }

                // Update App Registration ID
                var updateApId = "UPDATE wcmUserCred SET appRegId = @appRegId, updateDt = getdate(), updateID = @loginIdm WHERE loginIdm = @UserId";
                var parameter1 = new Dictionary<string, object>
                {
                    { "@appRegId", request.appRegId },
                    { "@loginIdm", request.userID },
                    { "@UserId", request.userID }
                };

                _dbHelper.WebExecute(updateApId, parameter1);

                var Detl = @"
                   SELECT 
                a.emplName AS emplName, 
                a.areaCode AS areaCode, 
                b.roleCode AS roleCode, 
                c.mobPagId AS mobPagId 
            FROM prmEmployee a WITH (NOLOCK)
            JOIN wcmEmplRole b WITH (NOLOCK) ON a.loginIdM = b.loginIdM
            JOIN wcmMobPgMas c WITH (NOLOCK) ON b.roleCode = c.roleCode
            WHERE 
                a.isActive = 'Y' 
                AND b.isActive = 'Y' 
                AND c.isActive = 'Y' 
                AND a.loginIdM = @userID";

                var param = new Dictionary<string, object> { { "@userID", request.userID } };
                var rset = _dbHelper.WebSessBean(Detl, param);

                if (rset == null || rset.Count == 0)
                {
                   // Console.WriteLine("❌ No detail records found for user.");
                    return Unauthorized("No role/page data found.");
                }

                string emplName = rset[0]["emplName"]?.ToString();
                string areaCode = rset[0]["areaCode"]?.ToString();

                var roles = new HashSet<string>();
                var pages = new HashSet<string>();

                foreach (var row in rset)
                {
                    var role = row["roleCode"]?.ToString();
                    var page = row["mobPagId"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(role))
                        roles.Add(role);

                    if (!string.IsNullOrWhiteSpace(page))
                        pages.Add(page);
                }
                /* Log for debugging
                Console.WriteLine("✅ Password match. Authentication successful.");
                Console.WriteLine("emplName: " + emplName);
                Console.WriteLine("areaCode: " + areaCode);
                Console.WriteLine("roles: " + string.Join(", ", roles));
                Console.WriteLine("pages: " + string.Join(", ", pages));
                // Structured response
                */
                return Ok(new
                {
                    msg = "Authentication successful",
                    data = new
                    {
                        emplName = emplName,
                        areaCode = areaCode,
                        roles = roles,
                        pages = pages
                    }
                });
            }
            catch (Exception ex)
            {
                //Console.WriteLine("🔥 Exception: " + ex.Message);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private string HashPassword(string password, string saltBase64)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(saltBase64))
                return null;

            byte[] saltBytes = Convert.FromBase64String(saltBase64);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            using (var sha1 = SHA1.Create())
            {
                // Step 1: SHA-1(salt + password)
                byte[] combined = new byte[saltBytes.Length + passwordBytes.Length];
                Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
                Buffer.BlockCopy(passwordBytes, 0, combined, saltBytes.Length, passwordBytes.Length);

                byte[] hash = sha1.ComputeHash(combined);

                // Step 2: Apply iteration hashing
                for (int i = 0; i < ITERATION_NUMBER; i++)
                {
                    hash = sha1.ComputeHash(hash);
                }

                return Convert.ToBase64String(hash);
            }
        }
    }

    public class AuthRequest
    {
        public string userID { get; set; }
        public string password { get; set; }
        public string appRegId { get; set; }
    }
}

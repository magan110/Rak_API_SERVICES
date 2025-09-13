using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RAKControllers.DataAccess;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// ---- CORS: allow your Flutter web origins (adjust as needed) ----
const string CorsPolicy = "AllowFlutterWeb";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, p => p
        .WithOrigins(
            "http://localhost:8520",     // flutter dev on localhost
            "http://10.4.64.23:8520",    // flutter served from this host:port (if applicable)
            "http://10.166.220.55:8081"  // IIS site hosting Flutter (if applicable)
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        // .AllowCredentials() // only if you actually use cookies; don't combine with AllowAnyOrigin
    );
});

// Connection strings
var connectionStrings = builder.Configuration.GetSection("ConnectionStrings").Get<Dictionary<string, string>>();

// DatabaseHelper
builder.Services.AddSingleton(provider =>
    new DatabaseHelper(connectionStrings["bwlive"]));

// HttpClient
builder.Services.AddHttpClient();

// Authentication (JWT)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "Birla White IT",

            // NOTE: this uses ServiceProvider inside configuration time (anti-pattern),
            // but keeping your original approach for now. Consider refactoring later.
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
                var userID = jwtToken.Claims.FirstOrDefault(c => c.Type == "userID")?.Value;

                if (userID != null)
                {
                    // Be careful: BuildServiceProvider creates a SECOND container.
                    // Prefer a scoped accessor or service locator set AFTER build.
                    var dbHelper = builder.Services.BuildServiceProvider().GetRequiredService<DatabaseHelper>();
                    var appregId = dbHelper.GetSecretKey(userID);
                    if (!string.IsNullOrEmpty(appregId))
                    {
                        return new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appregId)) };
                    }
                }
                throw new SecurityTokenInvalidSigningKeyException("Invalid PartnerID or SecretKey.");
            }
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    context.Token = authHeader.Replace("Bearer ", "");
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// -------- Pipeline ORDER (important for CORS) --------
app.UseStaticFiles();

// If you keep a custom auth middleware, put CORS BEFORE it and let OPTIONS through there
app.UseCors(CorsPolicy);

// Your custom middleware (must not block OPTIONS)
app.UseMiddleware<AuthenticationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Ensure preflight OPTIONS always returns with CORS headers
app.MapMethods("{*path}", new[] { "OPTIONS" }, () => Results.Ok()).RequireCors(CorsPolicy);

app.MapControllers();

app.Run();

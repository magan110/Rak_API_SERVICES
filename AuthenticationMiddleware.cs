using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RAKControllers.DataAccess;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DatabaseHelper _dbHelper;

    public AuthenticationMiddleware(RequestDelegate next, DatabaseHelper dbHelper)
    {
        _next = next;
        _dbHelper = dbHelper;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Bypass authentication for TokenController
        var currentPath = context.Request.Path.Value?.ToLower();
        if (currentPath?.StartsWith("/api/Auth", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Skip authentication for TokenController
            await _next(context);
            return;
        }
        if (currentPath?.StartsWith("/api/Painter/", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Skip authentication for TokenController
            await _next(context);
            return;
        }
        if (currentPath?.StartsWith("/api/Contractor/", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Skip authentication for TokenController
            await _next(context);
            return;
        }
        if (currentPath?.StartsWith("/api/RetailerOnboarding/", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Skip authentication for TokenController
            await _next(context);
            return;
        }
         if (currentPath?.StartsWith("/api/SampleLead/", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Skip authentication for TokenController
            await _next(context);
            return;
        }
        
        


        // Extract PartnerID and Authorization header
        var partnerId = context.Request.Headers["PartnerID"].FirstOrDefault();
        var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(partnerId) || string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 400; // Bad Request
            await context.Response.WriteAsync("PartnerID header and Authorization Bearer token are required.");
            return;
        }

        // Extract JWT token
        var jwtToken = authorizationHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        try
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            if (!jwtHandler.CanReadToken(jwtToken))
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("Invalid JWT token.");
                return;
            }

            // Validate the JWT token
            var token = jwtHandler.ReadJwtToken(jwtToken);
            var tokenPartnerId = token.Claims.FirstOrDefault(c => c.Type == "PartnerID")?.Value;

            if (string.IsNullOrEmpty(tokenPartnerId) || !string.Equals(tokenPartnerId, partnerId, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsync("Invalid or mismatched PartnerID in token.");
                return;
            }

            // Call the next middleware
            await _next(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync($"Token validation failed: {ex.Message}");
        }
    }
}

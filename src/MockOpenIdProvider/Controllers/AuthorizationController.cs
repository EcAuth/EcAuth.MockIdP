using Microsoft.AspNetCore.Mvc;
using MockOpenIdProvider.Models;
using Microsoft.EntityFrameworkCore;
using System.Web;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Headers;
using System.Text;

namespace MockOpenIdProvider.Controllers
{
    [Route("authorization")]
    public class AuthorizationController : Controller
    {
        private readonly IdpDbContext _context;
        private readonly PasswordHasher<MockIdpUser> _passwordHasher;

        public AuthorizationController(IdpDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<MockIdpUser>();
        }

        [HttpGet]
        /// <summary>Authorization リクエストを受信し、RedirectUri にリダイレクトします。</summary>
        public async Task<IActionResult> Index([FromHeader] string? authorization, [FromQuery] string redirect_uri, [FromQuery] string client_id, [FromQuery] string response_type, [FromQuery] string scope, [FromQuery] string state, [FromQuery] string nonce)
        {
            try
            {
                if (string.IsNullOrEmpty(authorization))
                {
                    // WWW-Authenticate ヘッダーを返す
                    Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Authorization Required\"");
                    return Unauthorized("Missing Authorization Header");
                }
                var authorizationHeaderValue = AuthenticationHeaderValue.Parse(authorization);
                if (!authorizationHeaderValue.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase))
                {
                    Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Authorization Required\"");
                    return Unauthorized("Invalid Authorization Scheme");
                }
                if (authorizationHeaderValue.Parameter == null)
                {
                    Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Authorization Required\"");
                    return Unauthorized("Missing Authorization Parameter");
                }
                var credentials = Encoding.UTF8
                    .GetString(Convert.FromBase64String(authorizationHeaderValue.Parameter))
                    .Split(':', 2);
                var email = credentials[0];
                var password = credentials[1];
                var User = await _context.Users.Where(
                    u => u.Email == email
                ).FirstOrDefaultAsync();
                if (User == null)
                {
                    Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Authorization Required\"");
                    return Unauthorized("Invalid username or password");
                }

                var result = _passwordHasher.VerifyHashedPassword(User, User.Password, password);
                if (result != PasswordVerificationResult.Success)
                {
                    Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Authorization Required\"");
                    return Unauthorized("Invalid username or password");
                }
                var Client = await _context.Clients.Where(
                    c => c.ClientId == client_id
                ).FirstOrDefaultAsync();

                if (Client?.RedirectUri == redirect_uri)
                {
                    var AuthorizationCode = new AuthorizationCode {
                        Code = Guid.NewGuid().ToString(),
                        CreatedAt = DateTime.UtcNow,
                        ExpiresIn = (int) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + 3600,
                        Used = false,
                        Client = Client,
                        ClientId = Client.Id,
                        User = User,
                        UserId = User.Id
                    };
                    await _context.AddAsync(AuthorizationCode);
                    await _context.SaveChangesAsync();
                    return Redirect($"{redirect_uri}?code={AuthorizationCode.Code}&state={state}&nonce={nonce}");
                }

                return Redirect(
                    $"{redirect_uri}" +
                    $"?error=invalid_request_uri" +
                    $"&error_description={HttpUtility.UrlEncode("The request_uri in the Authorization Request returns an error or contains invalid data.")}"
                );
            }
            catch (Exception ex)
            {
                // FIXME ログディレクトリ生成の抽象化
                var logsDir = "logs";
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                System.IO.File.AppendAllText(Path.Combine(logsDir, "exceptions.log"), ex.ToString() + Environment.NewLine);
                throw;
            }
        }
    }
}

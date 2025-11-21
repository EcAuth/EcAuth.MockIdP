using Microsoft.AspNetCore.Mvc;
using MockOpenIdProvider.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace MockOpenIdProvider.Controllers
{
    [Route("userinfo")]
    public class UserinfoController : Controller
    {
        private readonly IdpDbContext _context;

        public UserinfoController(IdpDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        /// <summary>アクセストークンを受け取り、ユーザ情報を返します。</summary>
        public Task<IActionResult> Index([FromHeader] string authorization)
        {
            try
            {
                if (string.IsNullOrEmpty(authorization))
                {
                    return Task.FromResult<IActionResult>(Json(new { error = "invalid_request" }));
                }
                var authorizationHeaderValue = AuthenticationHeaderValue.Parse(authorization);
                if (authorizationHeaderValue.Scheme != "Bearer")
                {
                    return Task.FromResult<IActionResult>(Json(new { error = "invalid_request" }));
                }
                var token = authorizationHeaderValue.Parameter;
                var AccessToken = _context.AccessTokens.Where(
                    at => at.Token == token
                ).FirstOrDefault();
                if (AccessToken == null)
                {
                    return Task.FromResult<IActionResult>(Json(new { error = "invalid_token" }));
                }
                if (AccessToken.ExpiresIn < (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds)
                {
                    return Task.FromResult<IActionResult>(Json(new { error = "invalid_token" }));
                }
                return Task.FromResult<IActionResult>(Json(new { sub = AccessToken.UserId }));
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

        [HttpGet("me")]
        /// <summary>テスト用ユーザーのメールアドレスを返します</summary>
        public Task<IActionResult> Me()
        {
            var user = _context.Users.FirstOrDefault();
            return Task.FromResult<IActionResult>(Json(new
            {
                email = user?.Email
            }
            ));
        }
    }
}

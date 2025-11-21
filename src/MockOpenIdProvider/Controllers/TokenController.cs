using IdpUtilities;
using Microsoft.AspNetCore.Mvc;
using MockOpenIdProvider.Models;
using Microsoft.EntityFrameworkCore;

namespace MockOpenIdProvider.Controllers
{
    [Route("token")]
    public class TokenController : Controller
    {
        private readonly IdpDbContext _context;

        public TokenController(IdpDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        /// <summary>Token リクエストを受信し、アクセストークンを返します。</summary>
        /// <remarks>
        /// - Client Authentication
        /// - grant_type のチェック
        /// - authorization_code のチェック
        /// - redirect_uri のチェック
        /// - refresh_token のチェック
        /// </remarks>
        public async Task<IActionResult> Index([FromForm] string grant_type, [FromForm] string? code = null, [FromForm] string? redirect_uri = null, [FromForm] string? client_id = null, [FromForm] string? client_secret = null, [FromForm] string? refresh_token = null)
        {
            if (string.IsNullOrEmpty(client_id) || string.IsNullOrEmpty(client_secret))
            {
                return Json(new { error = "invalid_request" });
            }
            
            if (!await _context.Clients.AnyAsync(c => c.ClientId == client_id && c.ClientSecret == client_secret))
            {
                return Json(new { error = "invalid_client" });
            }

            if (grant_type == "authorization_code")
            {
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(redirect_uri))
                {
                    return Json(new { error = "invalid_request" });
                }
                
                var AuthorizationCode = await _context.AuthorizationCodes.Where(
                    ac => ac.Code == code && ac.Client.ClientId == client_id && ac.Client.ClientSecret == client_secret
                ).FirstOrDefaultAsync();
                
                if (AuthorizationCode == null)
                {
                    return Json(new { error = "invalid_grant" });
                }
                
                if (AuthorizationCode.Used)
                {
                    return Json(new { error = "invalid_grant" });
                }
                
                if (AuthorizationCode.ExpiresIn < (int) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds)
                {
                    return Json(new { error = "invalid_grant" });
                }
                
                AuthorizationCode.Used = true;
                _context.Update(AuthorizationCode);
                
                var accessToken = RandomUtil.GenerateRandomBytes(32);
                var refreshTokenValue = RandomUtil.GenerateRandomBytes(32);
                
                var AccessToken = new AccessToken {
                    Token = accessToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresIn = (int) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + 3600,
                    Client = AuthorizationCode.Client,
                    ClientId = AuthorizationCode.ClientId,
                    User = AuthorizationCode.User,
                    UserId = AuthorizationCode.UserId
                };
                
                var refreshTokenEntity = new RefreshToken {
                    Token = refreshTokenValue,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresIn = (int) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + 86400 * 30, // 30日間有効
                    Client = AuthorizationCode.Client,
                    ClientId = AuthorizationCode.ClientId,
                    User = AuthorizationCode.User,
                    UserId = AuthorizationCode.UserId
                };
                
                await _context.AddAsync(AccessToken);
                await _context.AddAsync(refreshTokenEntity);
                await _context.SaveChangesAsync();
                
                return Json(
                    new {
                        access_token = accessToken,
                        token_type = "Bearer",
                        expires_in = 3600,
                        refresh_token = refreshTokenValue
                    }
                );
            }
            else if (grant_type == "refresh_token")
            {
                if (string.IsNullOrEmpty(refresh_token))
                {
                    return Json(new { error = "invalid_request" });
                }
                
                var existingRefreshToken = await _context.RefreshTokens.Where(
                    rt => rt.Token == refresh_token && rt.Client.ClientId == client_id
                ).Include(rt => rt.Client).Include(rt => rt.User).FirstOrDefaultAsync();
                
                if (existingRefreshToken == null)
                {
                    return Json(new { error = "invalid_grant" });
                }
                
                if (existingRefreshToken.ExpiresIn < (int) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds)
                {
                    // 古い refresh_token を削除
                    _context.RefreshTokens.Remove(existingRefreshToken);
                    await _context.SaveChangesAsync();
                    return Json(new { error = "invalid_grant" });
                }
                
                // 新しいアクセストークンを生成
                var newAccessToken = RandomUtil.GenerateRandomBytes(32);
                var AccessToken = new AccessToken {
                    Token = newAccessToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresIn = (int) (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + 3600,
                    Client = existingRefreshToken.Client,
                    ClientId = existingRefreshToken.ClientId,
                    User = existingRefreshToken.User,
                    UserId = existingRefreshToken.UserId
                };
                
                await _context.AddAsync(AccessToken);
                await _context.SaveChangesAsync();
                
                return Json(
                    new {
                        access_token = newAccessToken,
                        token_type = "Bearer",
                        expires_in = 3600,
                        refresh_token = refresh_token  // 同じリフレッシュトークンを返す
                    }
                );
            }
            else
            {
                return Json(new { error = "unsupported_grant_type" });
            }
        }
    }
}

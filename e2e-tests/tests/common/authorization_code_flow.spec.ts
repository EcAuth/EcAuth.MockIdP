import { test, expect } from '../../fixtures/organization.js';
import { request } from '@playwright/test';

test.describe.serial('認可コードフローのテストをします', () => {
  const scopes = 'openid profile email';

  test('MockOpenIdProvider の認可コードフローをテストをします', async ({
    organization,
    endpoints,
    clientId,
    clientSecret,
    redirectUri,
    testUser,
  }) => {
    // TODO: staging/production organization に client を登録後にスキップを解除
    test.skip(
      organization === 'staging' || organization === 'production',
      `${organization} organization はまだ client が未登録のためスキップ`
    );

    const tokenRequest = await request.newContext();

    // HTTP Basic 認証ヘッダーを設定
    const auth = Buffer.from(`${testUser.email}:${testUser.password}`).toString('base64');

    // 1. 認可エンドポイントへのアクセス（HTTP リクエストで302レスポンスを取得）
    const authUrl = `${endpoints.authorization}&client_id=${clientId}&redirect_uri=${encodeURIComponent(
      redirectUri
    )}&response_type=code&scope=${encodeURIComponent(scopes)}`;

    const authResponse = await tokenRequest.get(authUrl, {
      headers: {
        Authorization: `Basic ${auth}`,
      },
      maxRedirects: 0, // リダイレクトを自動追従しない
    });

    // Location ヘッダーから認可コードを取得
    const location = authResponse.headers()['location'];
    if (!location) {
      throw new Error('No location header found in authorization response');
    }
    const url = new URL(location);
    const code = url.searchParams.get('code');
    console.log(`[${organization}] Redirected to:`, location);
    console.log(`[${organization}] Authorization code:`, code);

    // 2. トークンエンドポイントでアクセストークン・リフレッシュトークン取得
    const formData = new URLSearchParams({
      client_id: clientId,
      client_secret: clientSecret,
      code: code ?? '',
      scope: scopes,
      redirect_uri: redirectUri,
      grant_type: 'authorization_code',
    });
    console.log(`[${organization}] Posting to:`, endpoints.token);
    console.log(`[${organization}] Form data:`, formData.toString());
    const response = await tokenRequest.post(endpoints.token, {
      data: formData.toString(),
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
    });
    const tokenResponseBody = await response.json();
    console.log(
      `[${organization}] Token response:`,
      JSON.stringify(tokenResponseBody, null, 2)
    );

    expect(tokenResponseBody.access_token).toBeTruthy();
    expect(tokenResponseBody.refresh_token).toBeTruthy();
    const refreshToken = tokenResponseBody.refresh_token;
    expect(tokenResponseBody.token_type).toBe('Bearer');

    // 3. ユーザー情報エンドポイントでユーザー情報取得
    const userInfoRequest = await request.newContext();
    const userInfoResponse = await userInfoRequest.get(endpoints.userinfo, {
      headers: {
        Authorization: `Bearer ${tokenResponseBody.access_token}`,
      },
    });

    const userInfoBody = await userInfoResponse.json();
    console.log(
      `[${organization}] UserInfo response:`,
      JSON.stringify(userInfoBody, null, 2)
    );
    expect(userInfoBody.sub).toBeTruthy();

    // 4. リフレッシュトークンで新しいアクセストークン取得
    const refreshFormData = new URLSearchParams({
      client_id: clientId,
      client_secret: clientSecret,
      scope: scopes,
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
    });
    const refreshTokenResponse = await tokenRequest.post(endpoints.token, {
      data: refreshFormData.toString(),
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
    });

    const refreshTokenResponseBody = await refreshTokenResponse.json();
    console.log(
      `[${organization}] Refresh token response:`,
      JSON.stringify(refreshTokenResponseBody, null, 2)
    );
    expect(refreshTokenResponseBody.access_token).toBeTruthy();
    expect(refreshTokenResponseBody.refresh_token).toBeTruthy();
    expect(refreshTokenResponseBody.token_type).toBe('Bearer');

    // 5. 新しいアクセストークンでユーザー情報取得
    test.step('Refresh Token で更新したアクセストークンでユーザー情報を取得します', async () => {
      const refreshTokenUserInfoRequest = await request.newContext();
      const refreshTokenUserInfoResponse =
        await refreshTokenUserInfoRequest.get(endpoints.userinfo, {
          headers: {
            Authorization: `Bearer ${refreshTokenResponseBody.access_token}`,
          },
        });
      const refreshUserInfoBody = await refreshTokenUserInfoResponse.json();
      console.log(
        `[${organization}] Refreshed UserInfo response:`,
        JSON.stringify(refreshUserInfoBody, null, 2)
      );
      expect(refreshUserInfoBody.sub).toBeTruthy();
      expect(refreshUserInfoBody.sub).toBe(userInfoBody.sub);
    });
  });
});

import { describe, expect, it } from 'vitest';
import {
  DEV,
  basicAuth,
  exchangeCode,
  getAuthorizationCode,
  postToken,
  request,
} from './helpers';

describe('認可コードフロー', () => {
  it('authorization → token → userinfo → refresh が一貫して動作する', async () => {
    const code = await getAuthorizationCode(DEV);
    const tokens = await exchangeCode(DEV, code);

    expect(tokens.access_token).toBeTruthy();
    expect(tokens.refresh_token).toBeTruthy();
    expect(tokens.token_type).toBe('Bearer');
    expect(tokens.expires_in).toBe(3600);

    const userinfo = await request(`/userinfo?org=${DEV.org}`, {
      headers: { Authorization: `Bearer ${tokens.access_token}` },
    });
    const user = await userinfo.json<{ sub: string }>();
    expect(user.sub).toBeTruthy();

    // リフレッシュして新しいアクセストークンを得る
    const refreshed = await postToken(DEV.org, {
      grant_type: 'refresh_token',
      refresh_token: tokens.refresh_token,
      client_id: DEV.clientId,
      client_secret: DEV.clientSecret,
    });
    const refreshedTokens = await refreshed.json<{
      access_token: string;
      refresh_token: string;
      token_type: string;
    }>();

    expect(refreshedTokens.access_token).toBeTruthy();
    expect(refreshedTokens.token_type).toBe('Bearer');
    // .NET 版と同じくリフレッシュトークンはローテーションしない
    expect(refreshedTokens.refresh_token).toBe(tokens.refresh_token);

    // 新しいアクセストークンでも同じ sub が返る
    const refreshedUserinfo = await request(`/userinfo?org=${DEV.org}`, {
      headers: { Authorization: `Bearer ${refreshedTokens.access_token}` },
    });
    const refreshedUser = await refreshedUserinfo.json<{ sub: string }>();
    expect(refreshedUser.sub).toBe(user.sub);
  });

  it('state と nonce をリダイレクト先へ引き回す', async () => {
    const params = new URLSearchParams({
      org: DEV.org,
      client_id: DEV.clientId,
      redirect_uri: DEV.redirectUri,
      response_type: 'code',
      scope: 'openid',
      state: 'Fe26.2**state-with-symbols**',
      nonce: 'nonce-value',
    });

    const response = await request(`/authorization?${params.toString()}`, {
      headers: { Authorization: basicAuth(DEV.email, DEV.password) },
    });

    expect(response.status).toBe(302);
    const location = new URL(response.headers.get('location') ?? '');
    expect(location.searchParams.get('state')).toBe('Fe26.2**state-with-symbols**');
    expect(location.searchParams.get('nonce')).toBe('nonce-value');
    expect(location.origin + location.pathname).toBe(DEV.redirectUri);
  });

  it('sub は同じユーザーなら毎回同じ値になる', async () => {
    const first = await exchangeCode(DEV, await getAuthorizationCode(DEV));
    const second = await exchangeCode(DEV, await getAuthorizationCode(DEV));

    const [firstUser, secondUser] = await Promise.all([
      request(`/userinfo?org=${DEV.org}`, {
        headers: { Authorization: `Bearer ${first.access_token}` },
      }).then((r) => r.json<{ sub: string }>()),
      request(`/userinfo?org=${DEV.org}`, {
        headers: { Authorization: `Bearer ${second.access_token}` },
      }).then((r) => r.json<{ sub: string }>()),
    ]);

    expect(firstUser.sub).toBe(secondUser.sub);
  });

  it('認可コードは 1 度しか使えない', async () => {
    const code = await getAuthorizationCode(DEV);

    const first = await exchangeCode(DEV, code);
    expect(first.access_token).toBeTruthy();

    const second = await postToken(DEV.org, {
      grant_type: 'authorization_code',
      code,
      redirect_uri: DEV.redirectUri,
      client_id: DEV.clientId,
      client_secret: DEV.clientSecret,
    });
    expect(await second.json()).toEqual({ error: 'invalid_grant' });
  });

  it('認可時と異なる redirect_uri ではトークンを発行しない', async () => {
    const code = await getAuthorizationCode(DEV);

    const response = await postToken(DEV.org, {
      grant_type: 'authorization_code',
      code,
      redirect_uri: 'https://attacker.example/callback',
      client_id: DEV.clientId,
      client_secret: DEV.clientSecret,
    });
    expect(await response.json()).toEqual({ error: 'invalid_grant' });
  });
});

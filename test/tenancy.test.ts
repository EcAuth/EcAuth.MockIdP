import { describe, expect, it } from 'vitest';
import {
  DEV,
  STAGING,
  basicAuth,
  exchangeCode,
  getAuthorizationCode,
  postToken,
  request,
} from './helpers';

describe('テナント解決', () => {
  it('org 未指定なら dev にフォールバックする', async () => {
    const params = new URLSearchParams({
      client_id: DEV.clientId,
      redirect_uri: DEV.redirectUri,
      response_type: 'code',
      scope: 'openid',
    });

    const response = await request(`/authorization?${params.toString()}`, {
      headers: { Authorization: basicAuth(DEV.email, DEV.password) },
    });

    expect(response.status).toBe(302);
  });

  it('X-Organization ヘッダーでもテナントを指定できる', async () => {
    const params = new URLSearchParams({
      client_id: STAGING.clientId,
      redirect_uri: STAGING.redirectUri,
      response_type: 'code',
      scope: 'openid',
    });

    const response = await request(`/authorization?${params.toString()}`, {
      headers: {
        Authorization: basicAuth(STAGING.email, STAGING.password),
        'X-Organization': STAGING.org,
      },
    });

    expect(response.status).toBe(302);
  });

  it('未知の org は invalid_organization', async () => {
    const response = await request('/userinfo?org=nonexistent');

    expect(response.status).toBe(400);
    expect(await response.json()).toMatchObject({ error: 'invalid_organization' });
  });

  it('設定が無いテナント（production）は invalid_organization', async () => {
    const response = await request('/userinfo?org=production');

    expect(response.status).toBe(400);
    expect(await response.json()).toMatchObject({ error: 'invalid_organization' });
  });
});

describe('テナント分離', () => {
  it('dev の認可コードは staging では使えない', async () => {
    const devCode = await getAuthorizationCode(DEV);

    const response = await postToken(STAGING.org, {
      grant_type: 'authorization_code',
      code: devCode,
      redirect_uri: DEV.redirectUri,
      client_id: STAGING.clientId,
      client_secret: STAGING.clientSecret,
    });

    expect(await response.json()).toEqual({ error: 'invalid_grant' });
  });

  it('dev のアクセストークンは staging の userinfo では使えない', async () => {
    const tokens = await exchangeCode(DEV, await getAuthorizationCode(DEV));

    const response = await request(`/userinfo?org=${STAGING.org}`, {
      headers: { Authorization: `Bearer ${tokens.access_token}` },
    });

    expect(await response.json()).toEqual({ error: 'invalid_token' });
  });

  it('dev の client_secret では staging のトークン要求は通らない', async () => {
    const stagingCode = await getAuthorizationCode(STAGING);

    const response = await postToken(STAGING.org, {
      grant_type: 'authorization_code',
      code: stagingCode,
      redirect_uri: STAGING.redirectUri,
      client_id: DEV.clientId,
      client_secret: DEV.clientSecret,
    });

    expect(await response.json()).toEqual({ error: 'invalid_client' });
  });

  it('テナントごとに sub が異なる', async () => {
    const devTokens = await exchangeCode(DEV, await getAuthorizationCode(DEV));
    const stagingTokens = await exchangeCode(STAGING, await getAuthorizationCode(STAGING));

    const [devUser, stagingUser] = await Promise.all([
      request(`/userinfo?org=${DEV.org}`, {
        headers: { Authorization: `Bearer ${devTokens.access_token}` },
      }).then((r) => r.json<{ sub: string }>()),
      request(`/userinfo?org=${STAGING.org}`, {
        headers: { Authorization: `Bearer ${stagingTokens.access_token}` },
      }).then((r) => r.json<{ sub: string }>()),
    ]);

    expect(devUser.sub).not.toBe(stagingUser.sub);
  });
});

describe('ヘルスチェック', () => {
  it.each(['/healthz', '/healthz/ready', '/healthz/live'])('%s は 200 を返す', async (path) => {
    const response = await request(path);

    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ status: 'healthy' });
  });
});

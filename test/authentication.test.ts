import { describe, expect, it } from 'vitest';
import { DEV, basicAuth, getAuthorizationCode, postToken, request } from './helpers';

const AUTHORIZE_QUERY = new URLSearchParams({
  org: DEV.org,
  client_id: DEV.clientId,
  redirect_uri: DEV.redirectUri,
  response_type: 'code',
  scope: 'openid',
}).toString();

describe('認可エンドポイントの Basic 認証', () => {
  it('Authorization ヘッダーが無ければ 401 と WWW-Authenticate を返す', async () => {
    const response = await request(`/authorization?${AUTHORIZE_QUERY}`);

    expect(response.status).toBe(401);
    expect(response.headers.get('WWW-Authenticate')).toBe(
      'Basic realm="Authorization Required"',
    );
  });

  it('Basic 以外のスキームは 401', async () => {
    const response = await request(`/authorization?${AUTHORIZE_QUERY}`, {
      headers: { Authorization: 'Bearer some-token' },
    });

    expect(response.status).toBe(401);
    expect(response.headers.get('WWW-Authenticate')).toBe(
      'Basic realm="Authorization Required"',
    );
  });

  it('パスワードが違えば 401', async () => {
    const response = await request(`/authorization?${AUTHORIZE_QUERY}`, {
      headers: { Authorization: basicAuth(DEV.email, 'wrong-password') },
    });

    expect(response.status).toBe(401);
  });

  it('存在しないユーザーは 401', async () => {
    const response = await request(`/authorization?${AUTHORIZE_QUERY}`, {
      headers: { Authorization: basicAuth('nobody@example.com', DEV.password) },
    });

    expect(response.status).toBe(401);
  });

  it('登録外の redirect_uri ではリダイレクトせず 400 を返す', async () => {
    const params = new URLSearchParams({
      org: DEV.org,
      client_id: DEV.clientId,
      redirect_uri: 'https://attacker.example/steal',
      response_type: 'code',
      scope: 'openid',
    });

    const response = await request(`/authorization?${params.toString()}`, {
      headers: { Authorization: basicAuth(DEV.email, DEV.password) },
    });

    // オープンリダイレクトを避けるため、302 ではなく 400 で返す
    expect(response.status).toBe(400);
    expect(response.headers.get('location')).toBeNull();
    expect(await response.json()).toMatchObject({ error: 'invalid_request_uri' });
  });

  it('未知の client_id は 400', async () => {
    const params = new URLSearchParams({
      org: DEV.org,
      client_id: 'unknown-client',
      redirect_uri: DEV.redirectUri,
      response_type: 'code',
      scope: 'openid',
    });

    const response = await request(`/authorization?${params.toString()}`, {
      headers: { Authorization: basicAuth(DEV.email, DEV.password) },
    });

    expect(response.status).toBe(400);
  });
});

describe('トークンエンドポイントのクライアント認証', () => {
  it('client_secret が違えば invalid_client', async () => {
    const code = await getAuthorizationCode(DEV);

    const response = await postToken(DEV.org, {
      grant_type: 'authorization_code',
      code,
      redirect_uri: DEV.redirectUri,
      client_id: DEV.clientId,
      client_secret: 'wrong-secret',
    });

    expect(await response.json()).toEqual({ error: 'invalid_client' });
  });

  it('client_id / client_secret が無ければ invalid_request', async () => {
    const response = await postToken(DEV.org, { grant_type: 'authorization_code' });

    expect(await response.json()).toEqual({ error: 'invalid_request' });
  });

  it('未対応の grant_type は unsupported_grant_type', async () => {
    const response = await postToken(DEV.org, {
      grant_type: 'client_credentials',
      client_id: DEV.clientId,
      client_secret: DEV.clientSecret,
    });

    expect(await response.json()).toEqual({ error: 'unsupported_grant_type' });
  });

  it('偽造した認可コードは invalid_grant', async () => {
    const response = await postToken(DEV.org, {
      grant_type: 'authorization_code',
      code: 'not-a-valid-token',
      redirect_uri: DEV.redirectUri,
      client_id: DEV.clientId,
      client_secret: DEV.clientSecret,
    });

    expect(await response.json()).toEqual({ error: 'invalid_grant' });
  });
});

describe('userinfo エンドポイント', () => {
  it('Authorization ヘッダーが無ければ invalid_request', async () => {
    const response = await request(`/userinfo?org=${DEV.org}`);

    expect(await response.json()).toEqual({ error: 'invalid_request' });
  });

  it('不正なトークンは invalid_token', async () => {
    const response = await request(`/userinfo?org=${DEV.org}`, {
      headers: { Authorization: 'Bearer bogus-token' },
    });

    expect(await response.json()).toEqual({ error: 'invalid_token' });
  });

  it('アクセストークンとして認可コードは使えない', async () => {
    const code = await getAuthorizationCode(DEV);

    const response = await request(`/userinfo?org=${DEV.org}`, {
      headers: { Authorization: `Bearer ${code}` },
    });

    expect(await response.json()).toEqual({ error: 'invalid_token' });
  });
});

import { describe, expect, it } from 'vitest';
import { DEV, DEV_FEDERATE, exchangeCode, getAuthorizationCode, postToken, request } from './helpers';

/**
 * dev テナントは 2 つのクライアントを持つ。
 *
 * EcAuth のフェデレーション E2E は `defaultuser@example.com` でログインしつつ、
 * federate クライアント（旧 MockIdP の InsertFederateClient マイグレーションが
 * 作っていたもの）でトークンを交換する。この組み合わせが壊れると
 * EcAuth 側の federate_authorization_code_flow.spec.ts が落ちる。
 */
describe('テナント内の複数クライアント', () => {
  it('federate クライアントでも認可コードフローが通る', async () => {
    const code = await getAuthorizationCode(DEV_FEDERATE);
    const tokens = await exchangeCode(DEV_FEDERATE, code);

    expect(tokens.access_token).toBeTruthy();
    expect(tokens.refresh_token).toBeTruthy();

    const userinfo = await request(`/userinfo?org=${DEV_FEDERATE.org}`, {
      headers: { Authorization: `Bearer ${tokens.access_token}` },
    });
    expect((await userinfo.json<{ sub: string }>()).sub).toBeTruthy();
  });

  it('2 つのクライアントは同じユーザーなので sub が一致する', async () => {
    const primary = await exchangeCode(DEV, await getAuthorizationCode(DEV));
    const federate = await exchangeCode(DEV_FEDERATE, await getAuthorizationCode(DEV_FEDERATE));

    const [primaryUser, federateUser] = await Promise.all([
      request(`/userinfo?org=${DEV.org}`, {
        headers: { Authorization: `Bearer ${primary.access_token}` },
      }).then((r) => r.json<{ sub: string }>()),
      request(`/userinfo?org=${DEV_FEDERATE.org}`, {
        headers: { Authorization: `Bearer ${federate.access_token}` },
      }).then((r) => r.json<{ sub: string }>()),
    ]);

    expect(primaryUser.sub).toBe(federateUser.sub);
  });

  it('既定クライアントの認可コードを federate クライアントで交換できない', async () => {
    const code = await getAuthorizationCode(DEV);

    const response = await postToken(DEV.org, {
      grant_type: 'authorization_code',
      code,
      redirect_uri: DEV.redirectUri,
      client_id: DEV_FEDERATE.clientId,
      client_secret: DEV_FEDERATE.clientSecret,
    });

    expect(await response.json()).toEqual({ error: 'invalid_grant' });
  });

  it('クライアントごとに client_secret が独立している', async () => {
    const code = await getAuthorizationCode(DEV_FEDERATE);

    const response = await postToken(DEV_FEDERATE.org, {
      grant_type: 'authorization_code',
      code,
      redirect_uri: DEV_FEDERATE.redirectUri,
      client_id: DEV_FEDERATE.clientId,
      client_secret: DEV.clientSecret,
    });

    expect(await response.json()).toEqual({ error: 'invalid_client' });
  });

  it('federate クライアントが未設定のテナントでは使えない', async () => {
    const params = new URLSearchParams({
      org: 'staging',
      client_id: 'federateclientid',
      redirect_uri: DEV_FEDERATE.redirectUri,
      response_type: 'code',
      scope: 'openid',
    });

    const response = await request(`/authorization?${params.toString()}`, {
      headers: { Authorization: `Basic ${btoa('staging-user@example.com:staging-password')}` },
    });

    expect(response.status).toBe(400);
  });
});

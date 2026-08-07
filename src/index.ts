import { Hono } from 'hono';
import type { MiddlewareHandler } from 'hono';
import { handleAuthorization } from './routes/authorization';
import { handleToken } from './routes/token';
import { handleUserinfo } from './routes/userinfo';
import { isKnownOrg, resolveTenant } from './tenants';
import type { AppBindings } from './types';

const app = new Hono<AppBindings>();

/**
 * テナント解決ミドルウェア。
 *
 * .NET 版の OrganizationMiddleware と同じ解決順序:
 *   クエリ `?org=` → ヘッダー `X-Organization` → 既定値 `dev`
 */
const resolveTenantMiddleware: MiddlewareHandler<AppBindings> = async (c, next) => {
  const org = c.req.query('org') ?? c.req.header('X-Organization') ?? 'dev';

  if (!isKnownOrg(org)) {
    return c.json(
      {
        error: 'invalid_organization',
        error_description: `Organization '${org}' not found`,
      },
      400,
    );
  }

  const tenant = await resolveTenant(c.env, org);
  if (!tenant) {
    return c.json(
      {
        error: 'invalid_organization',
        error_description: `Organization '${org}' is not configured`,
      },
      400,
    );
  }

  c.set('tenant', tenant);
  await next();
};

// ヘルスチェック。
// .NET 版は DbContext の疎通を確認していたが、Workers 版は起動時に接続を張る
// 外部依存が無いため常に healthy を返す。KV は遅延バインドで、疎通確認のために
// 書き込むと無料枠（1,000 writes/日）を消費するのでプローブしない。
app.get('/healthz', (c) => c.json({ status: 'healthy' }));
app.get('/healthz/ready', (c) => c.json({ status: 'healthy' }));
app.get('/healthz/live', (c) => c.json({ status: 'healthy' }));

// OAuth2 / OIDC エンドポイント。
// ミドルウェアは経路ごとに明示的に張る（登録順への依存を避けるため）。
app.use('/authorization', resolveTenantMiddleware);
app.use('/token', resolveTenantMiddleware);
app.use('/userinfo', resolveTenantMiddleware);

app.get('/authorization', handleAuthorization);
app.post('/token', handleToken);
app.get('/userinfo', handleUserinfo);

app.notFound((c) => c.json({ error: 'not_found' }, 404));

export default app;

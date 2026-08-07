import type { Env } from './env';
import type { Tenant } from './tenants';

/** Hono に渡す型引数。`tenant` はテナント解決ミドルウェアが設定する。 */
export type AppBindings = {
  Bindings: Env;
  Variables: {
    tenant: Tenant;
  };
};

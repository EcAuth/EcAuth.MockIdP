import { cloudflareTest } from '@cloudflare/vitest-pool-workers';
import { defineConfig } from 'vitest/config';

/**
 * テスト用のテナント設定。
 *
 * production を意図的に未設定のままにしてあるのは、「設定が無いテナントは
 * invalid_organization を返す」挙動をテストするため。
 */
const testBindings = {
  TOKEN_SIGNING_KEY: 'test-signing-key-for-unit-tests',

  MOCKIDP_DEV_CLIENT_ID: 'mockclientid',
  MOCKIDP_DEV_CLIENT_SECRET: 'mock-client-secret',
  MOCKIDP_DEV_CLIENT_NAME: 'MockClient',
  MOCKIDP_DEV_REDIRECT_URI: 'https://localhost:8081/v1/auth/callback',
  MOCKIDP_DEV_USER_EMAIL: 'defaultuser@example.com',
  MOCKIDP_DEV_USER_PASSWORD: 'password',

  // EcAuth のフェデレーションが使う 2 件目のクライアント
  MOCKIDP_DEV_FEDERATE_CLIENT_ID: 'federateclientid',
  MOCKIDP_DEV_FEDERATE_CLIENT_SECRET: 'federate-client-secret',
  MOCKIDP_DEV_FEDERATE_CLIENT_NAME: 'FederateClient',

  MOCKIDP_STAGING_CLIENT_ID: 'staging-mockclientid',
  MOCKIDP_STAGING_CLIENT_SECRET: 'staging-mock-client-secret',
  MOCKIDP_STAGING_CLIENT_NAME: 'StagingClient',
  MOCKIDP_STAGING_REDIRECT_URI: 'https://staging.example.test/v1/auth/callback',
  MOCKIDP_STAGING_USER_EMAIL: 'staging-user@example.com',
  MOCKIDP_STAGING_USER_PASSWORD: 'staging-password',
};

export default defineConfig({
  test: {
    // e2e-tests/ は Playwright 管理なので vitest の対象から外す。
    include: ['test/**/*.test.ts'],
  },
  plugins: [
    cloudflareTest({
      wrangler: { configPath: './wrangler.jsonc' },
      miniflare: { bindings: testBindings },
    }),
  ],
});

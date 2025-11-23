import { defineConfig, devices } from '@playwright/test';
import * as dotenv from 'dotenv';

/**
 * 環境変数を .env ファイルから読み込み
 */
dotenv.config();

/**
 * Playwright E2Eテスト設定
 *
 * 参考: https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  /**
   * テストディレクトリ
   */
  testDir: './tests',

  /**
   * 各テストファイルを並列実行
   */
  fullyParallel: true,

  /**
   * CI環境でのリトライ設定
   */
  retries: process.env.CI ? 2 : 0,

  /**
   * CI環境でのワーカー数（ローカルは半分のワーカー）
   */
  workers: process.env.CI ? 1 : undefined,

  /**
   * レポーター設定
   */
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
    ['json', { outputFile: 'test-results/results.json' }],
  ],

  /**
   * 共通設定
   */
  use: {
    /**
     * アクション実行時のタイムアウト（例: click, fill等）
     */
    actionTimeout: 10000,

    /**
     * ベースURL（環境変数から取得）
     */
    baseURL: process.env.MOCK_IDP_BASE_URL || 'https://localhost:9091',

    /**
     * トレース記録（失敗時のみ）
     */
    trace: 'retain-on-failure',

    /**
     * スクリーンショット（失敗時のみ）
     */
    screenshot: 'only-on-failure',

    /**
     * ビデオ記録（失敗時のみ）
     */
    video: 'retain-on-failure',

    /**
     * HTTPS証明書エラーを無視（自己署名証明書対応）
     */
    ignoreHTTPSErrors: true,
  },

  /**
   * プロジェクト定義（Organization別）
   */
  projects: [
    /**
     * dev organization
     * ローカル開発・テスト用
     */
    {
      name: 'dev',
      testMatch: ['**/common/**/*.spec.ts', '**/organizations/dev.spec.ts'],
      use: {
        ...devices['Desktop Chrome'],
      },
    },

    /**
     * staging organization
     * ステージング環境用
     */
    {
      name: 'staging',
      testMatch: [
        '**/common/**/*.spec.ts',
        '**/organizations/staging.spec.ts',
      ],
      use: {
        ...devices['Desktop Chrome'],
      },
    },

    /**
     * production organization
     * 本番環境用
     */
    {
      name: 'production',
      testMatch: [
        '**/common/**/*.spec.ts',
        '**/organizations/production.spec.ts',
      ],
      use: {
        ...devices['Desktop Chrome'],
      },
    },
  ],

  /**
   * ローカル開発サーバー設定（必要に応じて有効化）
   * MockOpenIdProvider をローカルで起動する場合はコメント解除
   */
  // webServer: {
  //   command: 'cd ../src/MockOpenIdProvider && dotnet run',
  //   url: 'https://localhost:9091',
  //   reuseExistingServer: !process.env.CI,
  //   ignoreHTTPSErrors: true,
  // },
});

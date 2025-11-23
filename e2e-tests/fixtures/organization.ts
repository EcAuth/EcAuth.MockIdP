import { test as base, expect } from '@playwright/test';

/**
 * Organization固有の設定を提供するfixture
 */
export type OrganizationFixture = {
  /**
   * Organization名（testInfo.project.nameから自動取得）
   */
  organization: string;

  /**
   * MockIdP のベースURL（環境変数から取得）
   */
  baseURL: string;

  /**
   * OAuth2 クライアントID
   */
  clientId: string;

  /**
   * OAuth2 クライアントシークレット
   */
  clientSecret: string;

  /**
   * リダイレクトURI
   */
  redirectUri: string;

  /**
   * テストユーザー情報
   */
  testUser: {
    email: string;
    password: string;
  };

  /**
   * Organization固有のエンドポイントURL群
   */
  endpoints: {
    authorization: string;
    token: string;
    userinfo: string;
  };
};

/**
 * Organization fixtureを含むtestオブジェクト
 */
export const test = base.extend<OrganizationFixture>({
  /**
   * Organization名を取得（testInfo.project.nameから）
   */
  organization: async ({}, use, testInfo) => {
    const organization = testInfo.project.name;
    await use(organization);
  },

  /**
   * ベースURLを取得（環境変数から）
   */
  baseURL: async ({}, use) => {
    const baseURL = process.env.MOCK_IDP_BASE_URL || 'https://localhost:9091';
    await use(baseURL);
  },

  /**
   * クライアントIDを取得
   * Organization固有の環境変数があればそれを使用、なければデフォルト値
   */
  clientId: async ({ organization }, use) => {
    const orgSpecificKey = `${organization.toUpperCase()}_CLIENT_ID`;
    const clientId =
      process.env[orgSpecificKey] || process.env.CLIENT_ID || 'mockclientid';
    await use(clientId);
  },

  /**
   * クライアントシークレットを取得
   * Organization固有の環境変数があればそれを使用、なければデフォルト値
   */
  clientSecret: async ({ organization }, use) => {
    const orgSpecificKey = `${organization.toUpperCase()}_CLIENT_SECRET`;
    const clientSecret =
      process.env[orgSpecificKey] ||
      process.env.CLIENT_SECRET ||
      'mock-client-secret';
    await use(clientSecret);
  },

  /**
   * リダイレクトURIを取得
   */
  redirectUri: async ({}, use) => {
    const redirectUri =
      process.env.REDIRECT_URI || 'https://localhost:8081/auth/callback';
    await use(redirectUri);
  },

  /**
   * テストユーザー情報を取得
   */
  testUser: async ({}, use) => {
    const testUser = {
      email: process.env.TEST_USER_EMAIL || 'defaultuser@example.com',
      password: process.env.TEST_USER_PASSWORD || 'password',
    };
    await use(testUser);
  },

  /**
   * Organization固有のエンドポイントURL群を生成
   */
  endpoints: async ({ baseURL, organization }, use) => {
    const endpoints = {
      authorization: `${baseURL}/authorization?org=${organization}`,
      token: `${baseURL}/token?org=${organization}`,
      userinfo: `${baseURL}/userinfo?org=${organization}`,
    };
    await use(endpoints);
  },
});

export { expect };

import { test, expect } from '../../fixtures/organization.js';

test.describe('dev organization 固有のテスト', () => {
  test('dev organization の設定を確認します', async ({ organization, baseURL, clientId }) => {
    expect(organization).toBe('dev');
    expect(baseURL).toBeTruthy();
    expect(clientId).toBeTruthy();

    console.log(`Organization: ${organization}`);
    console.log(`Base URL: ${baseURL}`);
    console.log(`Client ID: ${clientId}`);
  });

  // 将来的にdev固有のテストをここに追加
  // 例: dev-google, dev-line などの特定IdPシミュレーションテスト
});

import { test, expect } from '../../fixtures/organization.js';

test.describe('staging organization 固有のテスト', () => {
  // TODO: staging organization に client を登録後にスキップを解除
  test.skip('staging organization の設定を確認します', async ({ organization, baseURL, clientId }) => {
    expect(organization).toBe('staging');
    expect(baseURL).toBeTruthy();
    expect(clientId).toBeTruthy();

    console.log(`Organization: ${organization}`);
    console.log(`Base URL: ${baseURL}`);
    console.log(`Client ID: ${clientId}`);
  });

  // 将来的にstaging固有のテストをここに追加
  // 例: staging環境特有の設定確認テスト
});

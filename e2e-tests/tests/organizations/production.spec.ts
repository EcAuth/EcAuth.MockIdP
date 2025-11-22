import { test, expect } from '../../fixtures/organization.js';

test.describe('production organization 固有のテスト', () => {
  // TODO: production organization に client を登録後にスキップを解除
  test.skip('production organization の設定を確認します', async ({ organization, baseURL, clientId }) => {
    expect(organization).toBe('production');
    expect(baseURL).toBeTruthy();
    expect(clientId).toBeTruthy();

    console.log(`Organization: ${organization}`);
    console.log(`Base URL: ${baseURL}`);
    console.log(`Client ID: ${clientId}`);
  });

  // 将来的にproduction固有のテストをここに追加
  // 例: 本番環境特有の設定確認テスト、パフォーマンステスト
});

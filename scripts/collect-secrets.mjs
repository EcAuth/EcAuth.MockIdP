#!/usr/bin/env node
/**
 * `wrangler secret bulk` に流し込む JSON を環境変数から組み立てて stdout に出す。
 *
 *   op run --env-file=.env.workers.tpl -- node scripts/collect-secrets.mjs \
 *     | pnpm exec wrangler secret bulk
 *
 * op run が展開した値をパイプで直接 wrangler に渡すため、復号済みの値が
 * ディスクに残らない。op inject でファイルを生成する方式は使わないこと。
 */

const ORGS = ['DEV', 'STAGING', 'PRODUCTION'];
const TENANT_FIELDS = [
  'CLIENT_ID',
  'CLIENT_SECRET',
  'CLIENT_NAME',
  'REDIRECT_URI',
  'USER_EMAIL',
  'USER_PASSWORD',
  'USER_SUBJECT',
  // 2 件目のクライアント（EcAuth のフェデレーションが使う）
  'FEDERATE_CLIENT_ID',
  'FEDERATE_CLIENT_SECRET',
  'FEDERATE_CLIENT_NAME',
  'FEDERATE_REDIRECT_URI',
];

/** 必須キー。1 つでも欠けていれば投入を中止する。 */
const REQUIRED = ['TOKEN_SIGNING_KEY'];

const keys = [
  'TOKEN_SIGNING_KEY',
  ...ORGS.flatMap((org) => TENANT_FIELDS.map((field) => `MOCKIDP_${org}_${field}`)),
];

const secrets = {};
for (const key of keys) {
  const value = process.env[key];
  // 未解決の op:// 参照をそのまま投入してしまう事故を防ぐ
  if (value?.startsWith('op://')) {
    console.error(`Error: ${key} is still an unresolved 1Password reference. Run via \`op run\`.`);
    process.exit(1);
  }
  if (value) secrets[key] = value;
}

const missing = REQUIRED.filter((key) => !(key in secrets));
if (missing.length > 0) {
  console.error(`Error: required secrets are missing: ${missing.join(', ')}`);
  process.exit(1);
}

process.stdout.write(JSON.stringify(secrets));

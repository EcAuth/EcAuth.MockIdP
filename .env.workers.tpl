# Workers Secret 投入用の 1Password テンプレート。
#
# 使い方（README「シークレットの投入」も参照）:
#
#   eval $(op signin)
#   op run --env-file=.env.workers.tpl -- node scripts/collect-secrets.mjs \
#     | pnpm exec wrangler secret bulk
#
# op run はサブプロセスの環境変数にだけ値を展開するため、平文がディスクに
# 残らない。op inject でファイルを生成しないこと。

# トークン署名鍵（HS256）。
# 生成例: openssl rand -base64 48
TOKEN_SIGNING_KEY=op://EcAuth/mockidp-workers/token_signing_key

# --- dev テナント ---
MOCKIDP_DEV_CLIENT_ID=op://EcAuth/mockidp-dev/default_client_id
MOCKIDP_DEV_CLIENT_SECRET=op://EcAuth/mockidp-dev/default_client_secret
MOCKIDP_DEV_CLIENT_NAME=MockClient
MOCKIDP_DEV_REDIRECT_URI=op://EcAuth/mockidp-dev/redirect_uri
MOCKIDP_DEV_USER_EMAIL=op://EcAuth/mockidp-dev/default_user_email
MOCKIDP_DEV_USER_PASSWORD=op://EcAuth/mockidp-dev/default_user_password

# dev テナントの 2 件目のクライアント（EcAuth のフェデレーションが使う）。
# redirect_uri は MOCKIDP_DEV_REDIRECT_URI を引き継ぐ。
MOCKIDP_DEV_FEDERATE_CLIENT_ID=op://EcAuth/mockidp-dev/federate_client_id
MOCKIDP_DEV_FEDERATE_CLIENT_SECRET=op://EcAuth/mockidp-dev/federate_client_secret
MOCKIDP_DEV_FEDERATE_CLIENT_NAME=FederateClient

# --- staging テナント ---
MOCKIDP_STAGING_CLIENT_ID=op://EcAuth/mockidp-staging/default_client_id
MOCKIDP_STAGING_CLIENT_SECRET=op://EcAuth/mockidp-staging/default_client_secret
MOCKIDP_STAGING_CLIENT_NAME=StagingClient
MOCKIDP_STAGING_REDIRECT_URI=op://EcAuth/mockidp-staging/redirect_uri
MOCKIDP_STAGING_USER_EMAIL=op://EcAuth/mockidp-staging/default_user_email
MOCKIDP_STAGING_USER_PASSWORD=op://EcAuth/mockidp-staging/default_user_password

# --- production テナント ---
MOCKIDP_PRODUCTION_CLIENT_ID=op://EcAuth/mockidp-production/default_client_id
MOCKIDP_PRODUCTION_CLIENT_SECRET=op://EcAuth/mockidp-production/default_client_secret
MOCKIDP_PRODUCTION_CLIENT_NAME=ProductionClient
MOCKIDP_PRODUCTION_REDIRECT_URI=op://EcAuth/mockidp-production/redirect_uri
MOCKIDP_PRODUCTION_USER_EMAIL=op://EcAuth/mockidp-production/default_user_email
MOCKIDP_PRODUCTION_USER_PASSWORD=op://EcAuth/mockidp-production/default_user_password

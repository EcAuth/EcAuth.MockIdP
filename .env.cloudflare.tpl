# wrangler の認証情報を 1Password から注入するためのテンプレート。
#
#   eval $(op signin)
#   op run --env-file=.env.cloudflare.tpl -- pnpm exec wrangler deploy
#   op run --env-file=.env.cloudflare.tpl -- pnpm exec wrangler kv namespace create USED_CODES
#
# `wrangler login` の対話ログインの代わりに使う。復号値はサブプロセスの
# 環境変数にだけ渡り、ディスクには残らない。
#
# 注意: このファイルは **デプロイ用の認証情報** であり、Worker に注入する
# シークレット（.env.workers.tpl）とは別物。混ぜると Cloudflare の API トークンを
# Worker Secret として公開してしまうので絶対に統合しないこと。

CLOUDFLARE_ACCOUNT_ID=op://EcAuth/cloudflare-credentials/CLOUDFLARE_ACCOUNT_ID
CLOUDFLARE_API_TOKEN=op://EcAuth/cloudflare-credentials/CLOUDFLARE_API_TOKEN

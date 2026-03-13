# 1Password テンプレートファイル（production 環境用）
# 使用方法: op inject -i .env.prod.tpl -o .env

# Database Configuration (Azure SQL Database)
ConnectionStrings__MockIdpDbContext=op://EcAuth/ecauth-prod-sql/connection_string

# Application Settings
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# Default Organization (if not specified in request)
DEFAULT_ORGANIZATION=production

# DbInitializer Seed Data - Production Client & User
MOCKIDP_PRODUCTION_CLIENT_ID=op://EcAuth/mockidp-production/default_client_id
MOCKIDP_PRODUCTION_CLIENT_SECRET=op://EcAuth/mockidp-production/default_client_secret
MOCKIDP_PRODUCTION_CLIENT_NAME=ProductionClient
MOCKIDP_PRODUCTION_USER_EMAIL=op://EcAuth/mockidp-production/default_user_email
MOCKIDP_PRODUCTION_USER_PASSWORD=op://EcAuth/mockidp-production/default_user_password
MOCKIDP_PRODUCTION_REDIRECT_URI=op://EcAuth/mockidp-production/redirect_uri

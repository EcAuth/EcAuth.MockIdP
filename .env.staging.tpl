# 1Password テンプレートファイル（staging 環境用）
# 使用方法: op inject -i .env.staging.tpl -o .env

# Database Configuration (Azure SQL Database)
ConnectionStrings__MockIdpDbContext=op://EcAuth/ecauth-dev-sql/connection_string

# Application Settings
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://+:8080

# Default Organization (if not specified in request)
DEFAULT_ORGANIZATION=staging

# Migration Seed Data - Staging Client & User
MOCKIDP_STAGING_CLIENT_ID=op://EcAuth/mockidp-staging/default_client_id
MOCKIDP_STAGING_CLIENT_SECRET=op://EcAuth/mockidp-staging/default_client_secret
MOCKIDP_STAGING_CLIENT_NAME=StagingClient
MOCKIDP_STAGING_USER_EMAIL=op://EcAuth/mockidp-staging/default_user_email
MOCKIDP_STAGING_USER_PASSWORD=op://EcAuth/mockidp-staging/default_user_password
MOCKIDP_STAGING_REDIRECT_URI=op://EcAuth/mockidp-staging/redirect_uri

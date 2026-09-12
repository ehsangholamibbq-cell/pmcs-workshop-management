import "server-only";
import { betterAuth } from "better-auth";
import { genericOAuth } from "better-auth/plugins";
import { Pool } from "pg";
import { readAuthEnvironment } from "@/lib/auth-environment";

const configuration = readAuthEnvironment();
const globalForAuth = globalThis as typeof globalThis & { pmcsAuthPool?: Pool };
const pool = globalForAuth.pmcsAuthPool ?? new Pool({
  connectionString: configuration.databaseUrl,
  max: 10,
  idleTimeoutMillis: 30_000,
  connectionTimeoutMillis: 5_000,
});

if (process.env.NODE_ENV !== "production") {
  globalForAuth.pmcsAuthPool = pool;
}

const userFields = {
  name: "name",
  email: "email",
  emailVerified: "email_verified",
  image: "image",
  createdAt: "created_at",
  updatedAt: "updated_at",
} as const;

const sessionFields = {
  expiresAt: "expires_at",
  token: "token",
  createdAt: "created_at",
  updatedAt: "updated_at",
  ipAddress: "ip_address",
  userAgent: "user_agent",
  userId: "user_id",
} as const;

const accountFields = {
  accountId: "account_id",
  providerId: "provider_id",
  userId: "user_id",
  accessToken: "access_token",
  refreshToken: "refresh_token",
  idToken: "id_token",
  accessTokenExpiresAt: "access_token_expires_at",
  refreshTokenExpiresAt: "refresh_token_expires_at",
  scope: "scope",
  password: "password",
  createdAt: "created_at",
  updatedAt: "updated_at",
} as const;

export const auth = betterAuth({
  appName: "سامانه کنترل مدیریت پروژه",
  baseURL: configuration.webUrl,
  basePath: "/api/auth",
  secret: configuration.authSecret,
  database: pool,
  trustedOrigins: [configuration.webUrl],
  emailAndPassword: { enabled: false },
  user: {
    modelName: "bff_user",
    fields: userFields,
  },
  session: {
    modelName: "bff_session",
    fields: sessionFields,
    expiresIn: 60 * 60 * 12,
    updateAge: 60 * 60,
    freshAge: 60 * 10,
    cookieCache: { enabled: false },
  },
  account: {
    modelName: "bff_account",
    fields: accountFields,
    encryptOAuthTokens: true,
    storeStateStrategy: "database",
    accountLinking: {
      enabled: false,
      allowDifferentEmails: false,
      allowUnlinkingAll: false,
    },
  },
  verification: {
    modelName: "bff_verification",
    fields: {
      identifier: "identifier",
      value: "value",
      expiresAt: "expires_at",
      createdAt: "created_at",
      updatedAt: "updated_at",
    },
  },
  rateLimit: {
    enabled: true,
    storage: "database",
    modelName: "bff_rate_limit",
    fields: { lastRequest: "last_request" },
    window: 60,
    max: 60,
  },
  advanced: {
    useSecureCookies: configuration.secureCookies,
    cookiePrefix: "pmcs",
    defaultCookieAttributes: {
      httpOnly: true,
      secure: configuration.secureCookies,
      sameSite: "lax",
      path: "/",
    },
    database: {
      validateSchema: !configuration.isBuild,
    },
  },
  onAPIError: {
    errorURL: "/login",
  },
  plugins: configuration.isBuild ? [] : [
    genericOAuth({
      config: [
        {
          providerId: "keycloak",
          name: "ورود سازمانی",
          discoveryUrl: `${configuration.internalIssuer}/.well-known/openid-configuration`,
          authorizationUrl: `${configuration.publicIssuer}/protocol/openid-connect/auth`,
          tokenUrl: `${configuration.internalIssuer}/protocol/openid-connect/token`,
          userInfoUrl: `${configuration.internalIssuer}/protocol/openid-connect/userinfo`,
          endSessionEndpoint: `${configuration.publicIssuer}/protocol/openid-connect/logout`,
          postLogoutRedirectURI: `${configuration.webUrl}/login`,
          clientId: configuration.clientId,
          clientSecret: configuration.clientSecret,
          authentication: "basic",
          scopes: ["openid", "profile", "email", "pmcs-tenant", "pmcs-api-audience"],
          pkce: true,
          requireIdTokenVerification: true,
          requireEmailVerification: true,
        },
      ],
    }),
  ],
});

export const authEnvironment = configuration;

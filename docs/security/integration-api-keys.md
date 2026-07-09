# Integration API Keys

V1 integration API keys are a simple trust boundary for local development,
samples, and early integrations.

They authenticate integrations, not end users. An integration key identifies
the calling application, service, middleware, CLI, or agent bridge that is
allowed to ask Seneschal for capability decisions.

Integration keys scope which identities and capabilities an integration may
request. This prevents a caller with one integration key from submitting
arbitrary decision requests for unrelated identities or capabilities.

Direct calls to the evaluation endpoint must include:

```text
X-Seneschal-Api-Key: dev-sample-key
```

Example:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5077/evaluate `
  -Headers @{ "X-Seneschal-Api-Key" = "dev-sample-key" } `
  -ContentType "application/json" `
  -Body '{"identity":"Developer","capability":"DeployApplication","context":{"environment":"dev","resource":"sample-api"}}'
```

Checked-in keys are sample-only. Do not use checked-in API keys in production.

Production-grade secret storage, key rotation without restart, OIDC, mTLS,
HMAC request signing, replay protection, and authentication event auditing are
future hardening items.

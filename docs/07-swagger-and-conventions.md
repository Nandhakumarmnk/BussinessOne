# 07 · Swagger / OpenAPI & API Conventions

## 1. Swagger setup (ASP.NET Core 8)

We use **Swashbuckle.AspNetCore** to emit OpenAPI 3.0 and serve Swagger UI at `/swagger`.
The generated `swagger.json` is the **single source of truth** for the TypeScript client types
(`packages/types`) used by Web and Mobile.

```csharp
// Program.cs (excerpt)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo {
        Title = "Multi-Business ERP API", Version = "v1",
        Description = "Multi-tenant ERP for Transport, CCTV, Farm and Coconut businesses."
    });

    // JWT bearer
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Paste the JWT access token."
    });

    // Tenant header documented globally
    o.OperationFilter<BusinessIdHeaderOperationFilter>();   // adds X-Business-Id
    o.AddSecurityRequirement(new OpenApiSecurityRequirement {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference {
            Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });

    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "ERP.WebApi.xml"));
    o.SupportNonNullableReferenceTypes();
    o.UseAllOfToExtendReferenceSchemas();
});

var app = builder.Build();
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => { o.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP API v1");
                            o.DisplayRequestDuration(); });
}
```

`BusinessIdHeaderOperationFilter` adds the `X-Business-Id` header parameter to every
business-scoped operation (skipped for `/auth/*`, `/admin/*`, `/me`).

## 2. Client generation pipeline

```
backend build → emits swagger.json
   → CI step: openapi-typescript swagger.json -o packages/types/src/api.ts
   → CI step: generate api-client methods (orval / custom) from the same spec
   → web + mobile import @erp/types and @erp/api-client (no manual DTOs)
```

A CI **contract check** diffs the new `swagger.json` against the committed baseline and fails the
build on a breaking change unless the version is bumped — clients can never silently break.

## 3. Versioning

- URL versioning: `/api/v1`. Breaking changes → `/api/v2` (run side by side).
- Additive changes (new optional field, new endpoint) stay in `v1`.
- Deprecations advertised via `Deprecation` + `Sunset` response headers and Swagger `deprecated`.

## 4. Error model (RFC 7807 + machine codes)

All errors share one shape. Validation errors include `details[]`.

```jsonc
{ "error": {
    "code": "credit.limit_exceeded",     // stable, machine-readable, documented
    "message": "Customer credit limit exceeded by ₹5,000.",
    "status": 422,
    "details": [ { "field": "loadAmount", "message": "Exceeds available credit" } ],
    "correlationId": "0HMV9...",
    "timestamp": "2026-06-23T10:15:00Z" } }
```

Error code catalogue (excerpt — kept in `Application/Common/Errors`):

| Code | HTTP | Meaning |
|------|------|---------|
| `auth.invalid_credentials` | 401 | Bad login |
| `auth.token_expired` | 401 | Access token expired |
| `auth.forbidden` | 403 | Lacks permission |
| `tenant.business_required` | 400 | Missing `X-Business-Id` |
| `tenant.not_a_member` | 403 | User not member of business |
| `validation.failed` | 422 | Field validation (see details) |
| `resource.not_found` | 404 | Not found / filtered |
| `resource.conflict` | 409 | Duplicate code / state |
| `po.invalid_transition` | 409 | Illegal PO status change |
| `credit.limit_exceeded` | 422 | Domain rule |
| `rate.limited` | 429 | Too many requests |

## 5. Pagination, sorting, filtering

- Request: `?page=1&pageSize=20&sort=-loadDate,customerName&from=&to=`
- `pageSize` max 100 (server clamps). `sort` prefix `-` = descending.
- Response `meta`: `{ page, pageSize, total, totalPages }`.
- Cursor pagination (`?cursor=`) is used for `/sync/pull` (stable for large offline pulls).

## 6. Idempotency

- Mobile writes send `Idempotency-Key: <uuid>` (also persisted in `sync_client_requests`).
- The server dedupes: if the key was seen, it returns the original result instead of re-applying.
- Guarantees safe retries over flaky mobile networks.

## 7. Auth headers & token lifetimes

| Token | Lifetime | Transport |
|-------|----------|-----------|
| Access (JWT) | 15 min | `Authorization: Bearer` header |
| Refresh | 30 days, rotating | body of `/auth/refresh`; stored hashed server-side |

Claims: `sub` (userId), `tenant` (tenantId), `sa` (isSuperAdmin). Permissions are **not** packed
into the JWT (they vary per business); they are resolved per request from `user_businesses` +
`role_permissions`, cached briefly in memory.

## 8. CORS, rate limits, security headers

- CORS: allow only the Web origin(s) + Expo dev; credentials via Authorization header.
- Rate limiting: ASP.NET Core fixed-window limiter (e.g. 100 req/min/user; 10/min on `/auth/*`).
- Security headers: HSTS, X-Content-Type-Options, X-Frame-Options DENY, strict Referrer-Policy.
- Request size limits on upload endpoints; antivirus/extension allowlist on attachments (later).

## 9. Sample OpenAPI fragment (Load create)

```yaml
paths:
  /api/v1/transport/loads:
    post:
      tags: [Transport]
      summary: Create a transport load
      parameters:
        - { in: header, name: X-Business-Id, required: true, schema: { type: string, format: uuid } }
        - { in: header, name: Idempotency-Key, required: false, schema: { type: string, format: uuid } }
      security: [ { Bearer: [] } ]
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/CreateLoadRequest' }
      responses:
        '201': { description: Created,
                 content: { application/json: { schema: { $ref: '#/components/schemas/LoadResponse' } } } }
        '422': { description: Validation/domain error,
                 content: { application/json: { schema: { $ref: '#/components/schemas/ErrorEnvelope' } } } }
components:
  schemas:
    CreateLoadRequest:
      type: object
      required: [loadNumber, loadDate, loadAmount]
      properties:
        loadNumber: { type: string }
        customerId: { type: string, format: uuid }
        vehicleId:  { type: string, format: uuid }
        driverId:   { type: string, format: uuid }
        source: { type: string }
        destination: { type: string }
        loadDate: { type: string, format: date }
        loadAmount: { type: number }
        loadmanCharges: { type: number, default: 0 }
        fuelExpense: { type: number, default: 0 }
        maintenanceExpense: { type: number, default: 0 }
        driverCharges: { type: number, default: 0 }
        otherExpense: { type: number, default: 0 }
    LoadResponse:
      allOf:
        - $ref: '#/components/schemas/CreateLoadRequest'
        - type: object
          properties:
            id: { type: string, format: uuid }
            profit: { type: number, readOnly: true }
            status: { type: string }
```

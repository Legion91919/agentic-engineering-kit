---
name: secure-code-review
description: Review code for security with a checklist mapped to OWASP ASVS 4.0 and the OWASP Top 10 (2021), for .NET minimal APIs, React/Angular/Blazor front ends and Python/FastAPI - authN/authZ with object-level and tenant checks, input validation, output encoding, secrets, crypto, logging without personal data, SSRF, file upload, path traversal, deserialization, CORS/CSRF/cookies, rate limiting and error handling. Every item has the grep or test that proves it. Use before merge, before hand-off, or when asked "is this secure?".
---

# Secure code review

A finding without proof is an opinion. For every item below, run the grep or point at the test. A grep hit is a lead to read, not a verdict; a missing test is a finding.

Run greps from the repo root and skip build output: add `--exclude-dir={node_modules,bin,obj,dist,.venv}` (shortened to `$X` below).

## 1. Access control - Top 10 A01, ASVS V4
| Check | Proof |
|---|---|
| Every endpoint needs auth unless it is listed as public | .NET: `app.MapGroup(...).RequireAuthorization()` or a fallback policy; `grep -rn "AllowAnonymous" $X src/` and justify each hit. FastAPI: every router has `dependencies=[Depends(current_user)]`; `grep -rn "@router\.\(get\|post\|put\|delete\)" $X` and check each. Test: every route without a token returns 401 (enumerate `EndpointDataSource` or `app.routes`). |
| **Object-level authorization (IDOR)**: a record is loaded with the caller's ID or tenant in the query, not checked afterwards (or not at all) | `grep -rnE "FindAsync\(|FirstOrDefaultAsync\(x => x.Id ==|session.get\(" $X src/` and read each. Test per resource: user A asks for user B's ID and gets 404. |
| **Tenant isolation**: every query is filtered by tenant | .NET: EF global query filter on `TenantId`; `grep -rn "IgnoreQueryFilters" $X src/` must be empty or justified. Test: two tenants seeded, every list endpoint returns only the caller's rows. |
| Roles and policies checked per action, server-side | `grep -rnE "RequireRole|RequireAuthorization\(\"" $X src/`. Front-end route guards (`canActivate`, `<AuthorizeView>`, protected routes) are UX only; the API test must still return 403. |

## 2. Input and output - A03, ASVS V5
| Check | Proof |
|---|---|
| Request bodies validated (type, length, range, allowed values) | .NET: FluentValidation or `[Range]`/`[MaxLength]` plus a validation filter; FastAPI: Pydantic models with `Field(max_length=...)`. Test: oversize and out-of-range payloads return 400/422. |
| No SQL built from strings | `grep -rnE "FromSqlRaw|ExecuteSqlRaw|\\$\"SELECT|f\"SELECT|\.execute\(f\"|% \(" $X src/` must be empty or use parameters (`FromSqlInterpolated`, `:param`). |
| No HTML built from user input | React: `grep -rn "dangerouslySetInnerHTML" $X web/`. Angular: `grep -rn "bypassSecurityTrust\|innerHTML\]" $X web/`. Blazor: `grep -rn "MarkupString" $X src/`. Each hit needs a sanitiser and a test with `<img src=x onerror=alert(1)>`. |
| No shell or eval with user input | `grep -rnE "Process.Start|subprocess\.|os\.system|eval\(|exec\(|new Function\(" $X src/ web/`. |
| Content-Security-Policy set | Test: a response from `/` has a `Content-Security-Policy` header without `unsafe-inline` for scripts. |

## 3. Secrets and crypto - A02, ASVS V2, V6
| Check | Proof |
|---|---|
| No secrets in code or config | `gitleaks detect --no-banner` and `python3 tools/privacy_guard.py --all` are green. `appsettings*.json` and `.env.example` hold placeholders only. |
| Passwords hashed with a slow, salted hash | .NET: ASP.NET Core Identity `PasswordHasher` (PBKDF2) or Argon2; Python: `argon2-cffi` or `passlib[bcrypt]`. `grep -rniE "MD5|SHA1|SHA256\.(Create|HashData).*[Pp]assword|hashlib\.(md5|sha1|sha256)\(.*pass" $X src/` must be empty. |
| Tokens and IDs from a CSPRNG | `grep -rnE "new Random\(|Math.random\(|random\.(random|randint|choice)\(" $X src/ web/` must not generate tokens, keys or reset codes. Use `RandomNumberGenerator`, `crypto.getRandomValues`, `secrets`. |
| JWTs validated fully | `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all true; `grep -rn "= false" $X src/ | grep -i validate` is empty. Python: `jwt.decode(..., algorithms=["RS256"], audience=...)`, never `options={"verify_signature": False}`. Test: expired and wrong-audience tokens return 401. |
| Tokens not stored in `localStorage` for sensitive apps | `grep -rn "localStorage.setItem" $X web/`; prefer an `HttpOnly` cookie or a backend-for-frontend. |

## 4. Logging and errors - A09, ASVS V7
| Check | Proof |
|---|---|
| No personal data, tokens or secrets in logs | `grep -rnE "Log(Information|Warning|Error|Debug)\(.*(Email|Phone|IdNumber|Password|Token|Authorization)|logger\.\w+\(.*(email|phone|password|token)" $X src/`. Test: a log-capturing test runs a request with a known email and asserts it never appears. |
| Security events logged (login fail, access denied, admin action) with actor and correlation ID | Test: a 403 produces one structured log event with `userId` and `traceId`. |
| Errors return ProblemDetails without stack traces | .NET: `app.UseExceptionHandler()` + `AddProblemDetails()`, and `UseDeveloperExceptionPage` only under `IsDevelopment()`. FastAPI: `debug=False` and a generic exception handler. Test: a forced exception returns 500 with no `   at ` or `Traceback` in the body. |

## 5. Server-side requests and files - A10, A01, ASVS V12
| Check | Proof |
|---|---|
| **SSRF**: no outbound call to a URL taken from the request | `grep -rnE "HttpClient.*(GetAsync|PostAsync)\(.*(request|dto|url)|httpx\.(get|post)\(|requests\.(get|post)\(" $X src/`. Use an allow-list of hosts. Test: `http://169.254.169.254/` and `http://localhost` are refused. |
| **File upload**: size limit, type checked by content (magic bytes), stored outside web root with a generated name | `grep -rnE "IFormFile|UploadFile" $X src/` and read each. Test: a 20 MB file and a `.exe` renamed `.pdf` are rejected. |
| **Path traversal**: user input never joins a file path | `grep -rnE "Path.Combine\(.*(request|name|file)|os.path.join\(.*(request|name|file)|open\(.*request" $X src/`. Test: `../../etc/passwd` as a file name returns 400. |

## 6. Deserialization and data - A08, ASVS V5.5
| Check | Proof |
|---|---|
| No unsafe deserializers | `grep -rnE "BinaryFormatter|TypeNameHandling\.(All|Auto|Objects)|pickle\.loads?|yaml\.load\(|jsonpickle" $X src/` must be empty (use `System.Text.Json`, `yaml.safe_load`). |
| Mass assignment blocked | Endpoints bind to request DTOs, never to EF entities or SQLAlchemy models. `grep -rnE "Map(Post|Put)\(.*\((\w+Entity|\w+Model) " $X src/`. |

## 7. Browser and session - A05, A07, ASVS V3, V14
| Check | Proof |
|---|---|
| CORS lists exact origins | `grep -rnE "AllowAnyOrigin|allow_origins=\[\"\*\"\]|SetIsOriginAllowed\(_ => true\)" $X src/` must be empty, especially with credentials. Test: a preflight from an unknown origin gets no `Access-Control-Allow-Origin`. |
| Cookies are `Secure`, `HttpOnly`, `SameSite=Lax` or `Strict` | Test: inspect `Set-Cookie` on login. `grep -rn "SameSiteMode.None\|HttpOnly = false\|httponly=False" $X src/`. |
| CSRF protection when auth is cookie-based | .NET: antiforgery on form posts (`app.UseAntiforgery()`); Angular: `HttpClientXsrfModule`; FastAPI: double-submit token. Test: a cookie-authenticated POST without the token returns 400/403. |
| HTTPS and HSTS outside development | `app.UseHttpsRedirection()` and `UseHsts()`; test the header exists. |

## 8. Abuse limits - A04, ASVS V11, V13
| Check | Proof |
|---|---|
| Rate limits on login, sign-up, password reset, expensive and AI endpoints | .NET 8: `AddRateLimiter` + `.RequireRateLimiting("...")`; FastAPI: `slowapi` or a gateway rule. Test: request N+1 in the window returns 429. |
| Request size, page size and timeouts capped | `MaxRequestBodySize`, `take = Math.Min(take, 100)`, `HttpClient.Timeout`. Test: `?pageSize=100000` is clamped. |

## Reporting
For each finding: severity (Critical, High, Medium, Low), category (for example `A01 Broken Access Control / ASVS V4.2.1`), `file:line`, the proof you ran, and the fix. Turn each missing test into a failing test in the next slice. Record accepted risks in `docs/THREAT-MODEL.md`.

## Done when
Every section has been run, every hit is resolved or justified, and each fix has a test that would fail if the fix were removed.

# 🔐 HotelListing.API - Security Checklist for Developers

**Purpose:** Quick reference for implementing and maintaining API security  
**Audience:** Development Team  
**Last Updated:** August 16, 2026

---

## Before Committing Code

### Authentication & Authorization
- [ ] All endpoints that modify data require `[Authorize]`
- [ ] Admin-only endpoints require `[Authorize(Roles = "Admin")]`
- [ ] `UseAuthentication()` comes before `UseAuthorization()` in middleware
- [ ] No credentials or API keys in code
- [ ] JWT tokens have expiration time
- [ ] Tokens validated strictly (lifetime, issuer, audience)

### Input Validation
- [ ] All POST/PUT requests validated with FluentValidation
- [ ] String lengths limited
- [ ] Special characters restricted appropriately
- [ ] Numeric ranges enforced
- [ ] No SQL injection possible
- [ ] No null values where not allowed

### Error Handling
- [ ] Exception handler middleware configured
- [ ] Stack traces never exposed to clients
- [ ] Development details hidden in production
- [ ] Proper HTTP status codes used
- [ ] Error messages are generic (don't leak info)

### Configuration
- [ ] No secrets in appsettings.json
- [ ] JWT key in User Secrets (dev) or Key Vault (prod)
- [ ] Connection strings not in version control
- [ ] AllowedHosts configured appropriately
- [ ] Environment-specific config files

### Logging
- [ ] No passwords logged
- [ ] No API keys logged
- [ ] No auth tokens logged
- [ ] No personal data logged unnecessarily
- [ ] Failed auth attempts logged
- [ ] Authorization failures logged

---

## Before Deploying

### Code Review
- [ ] Security peer review completed
- [ ] No credentials in commit history
- [ ] No vulnerable dependencies
- [ ] Authorization checks implemented
- [ ] Input validation complete
- [ ] Error handling appropriate

### Testing
- [ ] Authentication tests passing
- [ ] Authorization tests passing
- [ ] Validation tests passing
- [ ] Security headers verified
- [ ] CORS policy verified
- [ ] Rate limiting verified

### Configuration
- [ ] Production secrets in Key Vault
- [ ] Database connection string secured
- [ ] HTTPS enforced
- [ ] HSTS headers enabled
- [ ] AllowedHosts configured
- [ ] CORS origins restricted

### Deployment
- [ ] Secrets not in appsettings.Production.json
- [ ] Environment variables set correctly
- [ ] HTTPS certificate valid
- [ ] Firewall rules configured
- [ ] WAF (Web Application Firewall) configured
- [ ] Logging/monitoring enabled

---

## Code Review Checklist

When reviewing PRs with security implications:

### Authentication Changes
- [ ] No disabled JWT validation
- [ ] No hardcoded credentials
- [ ] Token expiration enforced
- [ ] Signing key properly secured
- [ ] No custom crypto implementation

### Authorization Changes
- [ ] Server-side checks present
- [ ] Not relying on client-side checks
- [ ] Admin operations protected
- [ ] User ownership validated
- [ ] Role-based access control properly implemented

### Data Access Changes
- [ ] Using parameterized queries (EF Core)
- [ ] No string concatenation in queries
- [ ] Input sanitized
- [ ] Output encoded where necessary
- [ ] Proper error handling

### Configuration Changes
- [ ] No secrets committed
- [ ] Environment-specific configs
- [ ] Connection strings secured
- [ ] Logging doesn't expose secrets
- [ ] .gitignore updated if needed

---

## Security Testing Checklist

### Authentication Tests
```csharp
[Fact]
public async Task GetCountries_WithoutToken_ShouldReturnUnauthorized()
{
    // Anonymous access to protected endpoint should fail
}

[Fact]
public async Task CreateCountry_WithoutToken_ShouldReturnUnauthorized()
{
    // Must have valid JWT
}

[Fact]
public async Task CreateCountry_WithExpiredToken_ShouldReturnUnauthorized()
{
    // Expired tokens should be rejected
}
```

### Authorization Tests
```csharp
[Fact]
public async Task CreateCountry_WithUserRole_ShouldForbidden()
{
    // Only admins can create
}

[Fact]
public async Task DeleteCountry_WithoutAdminRole_ShouldForbidden()
{
    // Only admins can delete
}
```

### Validation Tests
```csharp
[Fact]
public async Task CreateCountry_WithLongName_ShouldFail()
{
    // Enforce max length
}

[Fact]
public async Task CreateCountry_WithInvalidShortName_ShouldFail()
{
    // ShortName must be 2 uppercase letters
}
```

### Error Handling Tests
```csharp
[Fact]
public async Task GetCountry_InvalidId_ShouldReturn404()
{
    // Generic error, no stack trace
}

[Fact]
public async Task GetCountry_ServerError_ShouldNotExposStackTrace()
{
    // Stack trace hidden from clients
}
```

---

## Common Security Mistakes to Avoid

### ❌ DON'T

```csharp
// ❌ Hardcoded credentials
var password = "admin123";

// ❌ No authorization check
public ActionResult DeleteCountry(int id)
{
    // Anyone can delete!
}

// ❌ Exposing exception details
catch (Exception ex)
{
    return BadRequest(ex.Message); // Shows stack trace!
}

// ❌ No input validation
public ActionResult CreateHotel([FromBody] Hotel hotel)
{
    hotels.Add(hotel); // What if hotel.Name is 10000 chars?
}

// ❌ Secrets in appsettings
{
  "Jwt": {
    "Key": "super-secret-key"  // NEVER!
  }
}

// ❌ String concatenation in queries
var sql = $"SELECT * FROM Countries WHERE Id = {id}"; // SQL Injection!

// ❌ No HTTPS enforcement
// Just hope it's HTTPS...

// ❌ AllowedHosts = "*"
{
  "AllowedHosts": "*"  // Host header injection!
}

// ❌ Logging sensitive data
logger.LogInformation($"Login attempt: {username}:{password}"); // Password logged!
```

### ✅ DO

```csharp
// ✅ Use User Secrets
var jwtKey = configuration["Jwt:Key"];

// ✅ Require authorization
[Authorize(Roles = "Admin")]
public ActionResult DeleteCountry(int id)
{
    // Only admins allowed
}

// ✅ Handle exceptions properly
catch (Exception ex)
{
    logger.LogError(ex, "Error message");
    return StatusCode(500, new { message = "An error occurred" }); // No details!
}

// ✅ Validate input
[Authorize(Roles = "Admin")]
public ActionResult CreateHotel([FromBody] Hotel hotel)
{
    // FluentValidation validates automatically
}

// ✅ Secrets in Key Vault
{
  "Jwt": {
    // Key comes from environment or Key Vault
  }
}

// ✅ Use parameterized queries
var hotel = await _context.Hotels
    .Where(h => h.Id == id)
    .FirstOrDefaultAsync(); // EF Core parameterizes

// ✅ Enforce HTTPS
app.UseHsts();
app.UseHttpsRedirection();

// ✅ Restrict AllowedHosts
{
  "AllowedHosts": "yourdomain.com"
}

// ✅ Don't log sensitive data
logger.LogInformation($"Login attempt for username: {username}"); // Password omitted
```

---

## Deployment Security Checklist

### Before Production Deployment

**Infrastructure**
- [ ] HTTPS certificate installed
- [ ] SSL/TLS 1.2+ configured
- [ ] Certificate not self-signed
- [ ] Certificate valid for domain
- [ ] Certificate renewal automated

**Application**
- [ ] Build in Release configuration
- [ ] No debug symbols in production
- [ ] Unused endpoints removed
- [ ] Error handling configured
- [ ] Logging configured

**Secrets Management**
- [ ] All secrets in Key Vault/Secrets Manager
- [ ] No secrets in configuration files
- [ ] Database password secured
- [ ] API keys secured
- [ ] JWT key secured
- [ ] Encryption keys secured

**Database**
- [ ] Strong password required
- [ ] Principle of least privilege
- [ ] Connection string encrypted
- [ ] SQL Server/DB firewall configured
- [ ] Automated backups configured

**Monitoring**
- [ ] Application logging enabled
- [ ] Security events logged
- [ ] Performance monitoring enabled
- [ ] Alert thresholds configured
- [ ] Incident response plan documented

**Compliance**
- [ ] Data protection policy established
- [ ] PII handling documented
- [ ] Data retention policy set
- [ ] Access control documented
- [ ] Incident response procedure

---

## Security Training References

### Must Read
- [ ] OWASP Top 10 2021
- [ ] Microsoft ASP.NET Core Security docs
- [ ] NIST Cybersecurity Framework
- [ ] CWE/SANS Top 25

### OWASP Top 10 Mapping

| Risk | How We Mitigate |
|------|-----------------|
| A01:2021 - Broken Access Control | `[Authorize]` attributes, role checks |
| A02:2021 - Cryptographic Failures | HTTPS, secrets in Key Vault |
| A03:2021 - Injection | EF Core parameterized queries, input validation |
| A04:2021 - Insecure Design | Security by design, reviews |
| A05:2021 - Security Misconfiguration | Secure defaults, environment configs |
| A06:2021 - Vulnerable Components | Keep NuGet packages updated |
| A07:2021 - Identification/Auth | JWT, token validation, rate limiting |
| A08:2021 - Software/Data Integrity | Only use official NuGet sources |
| A09:2021 - Logging/Monitoring | Centralized logging, alerts |
| A10:2021 - SSRF | URL validation, network policies |

---

## Incident Response

### If Credentials Are Leaked
1. [ ] Immediately rotate affected credentials
2. [ ] Invalidate all active tokens
3. [ ] Reset user passwords if needed
4. [ ] Check logs for unauthorized access
5. [ ] Audit data for modifications
6. [ ] Notify affected users
7. [ ] Post-incident review

### If Vulnerability Is Discovered
1. [ ] Patch immediately if critical
2. [ ] Update dependencies
3. [ ] Audit other uses of vulnerable code
4. [ ] Deploy patched version
5. [ ] Monitor for exploitation
6. [ ] Document in incident log

### If Unauthorized Access Is Detected
1. [ ] Immediately disable affected account
2. [ ] Review access logs
3. [ ] Check for data exfiltration
4. [ ] Notify incident response team
5. [ ] Document timeline
6. [ ] Preserve evidence
7. [ ] Notify affected parties

---

## Questions?

| Question | Reference |
|----------|-----------|
| "How do I implement authentication?" | SECURITY_REMEDIATION_GUIDE.md |
| "What security issues exist?" | SECURITY_AUDIT_REPORT.md |
| "How do I deploy securely?" | This checklist |
| "What are the risks?" | PROJECT_REVIEW_REPORT.md |

---

**Remember:** Security is everyone's responsibility!

*Security Checklist - HotelListing.API*  
*For Development Team Use*


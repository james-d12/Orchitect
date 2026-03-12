# JWT Best Practices

This document consolidates up-to-date guidance for **securely issuing, validating, and using JSON Web Tokens (JWTs)**, drawing from both real-world best practices and formal recommendations in RFC 8725.

---

## 📌 1. Understand JWT Fundamentals

JWTs are a **compact, URL-safe token format** that can be:

- **Signed** (JWS — integrity/authentication)
- **Encrypted** (JWE — confidentiality)
- Or both (nested JWS/JWE) :contentReference[oaicite:2]{index=2}

**Important:** JWT is a format, not a security mechanism by itself. Its safety depends on **correct use and validation**. :contentReference[oaicite:3]{index=3}

---

## 🔐 2. Always Validate Cryptographic Protections

### 🔍 2.1 Validate All Crypto Operations

For every received JWT:

- **Verify signatures** for all layers (outer and inner if nested).
- **Reject the token entirely if any cryptographic operation fails**.
- This includes signature checks, decryption checks, or any algorithm use. :contentReference[oaicite:4]{index=4}

This is the core of Section 3.3 of RFC 8725, ensuring cryptographic protections are assured rather than assumed. :contentReference[oaicite:5]{index=5}

### 🛡 2.2 Algorithm Allow Lists

- **Never trust the `alg` header blindly.**
- Use an **allow list** of acceptable algorithms and reject tokens specifying anything outside that set.
- Avoid algorithm downgrade or `none` attacks by explicit validation. :contentReference[oaicite:6]{index=6}

---

## 🔑 3. Cryptographic Algorithm Selection

Prefer modern, strong algorithms:

| Category | Recommendation                                                                 |
|----------|-------------------------------------------------------------------------------|
| Best     | **EdDSA (Ed25519/Ed448)** — excellent security and performance               |
| Good     | **ECDSA (ES256)** — strong elliptic-curve signature                           |
| Widely supported | **RS256** — acceptable, but heavier                                |
| Caution  | **HS256** (symmetric) — only if necessary, key sharing is risky              |

Symmetric keys (HMAC) require all parties to share the same secret, increasing exposure. :contentReference[oaicite:7]{index=7}

---

## 👀 4. Validate Standard Claims

### 🆔 4.1 Issuer (`iss`)

- Always check that the `iss` claim matches an **expected trusted issuer**.
- This prevents **token substitution attacks** (accepting tokens from another issuer). :contentReference[oaicite:8]{index=8}

### 👥 4.2 Audience (`aud`)

- Confirm that the token’s `aud` includes the **service or API** receiving it.
- Reject tokens intended for different audiences. :contentReference[oaicite:9]{index=9}

### ⏰ 4.3 Time-Based Claims

Enforce time validity:

- `exp` — expiration time
- `nbf` — not-before time
- `iat` — issued-at time

Use a small allowed **clock skew** (seconds) to account for clock drift. :contentReference[oaicite:10]{index=10}

---

## 🔗 5. Validate Claim Usage and Intent

### 🔎 5.1 Distinguish Token Types

Ensure JWTs are used as intended:

- Access tokens must **not be accepted as ID tokens**.
- Use claims such as `scope`, `typ`, or custom fields to confirm intent. :contentReference[oaicite:11]{index=11}

### ❌ 5.2 Don’t Trust Received Claims Without Context

- Claims from the token should **not be blindly trusted for authorization decisions without verification** of token integrity and source. :contentReference[oaicite:12]{index=12}

---

## 🔐 6. Key Management

### 🌐 6.1 Use JWKS for Public Key Distribution

- Retrieve public keys from a trusted **JWKS endpoint**.
- Cache keys and support automatic rotation. :contentReference[oaicite:13]{index=13}

### 🪪 6.2 Validate `kid`, `jku`, and Key Inputs

- If a token header contains references to keys (like `kid` or `jku`), ensure they are **verified against expected trusted issuers** and not adversarial. :contentReference[oaicite:14]{index=14}

---

## 🚪 7. Minimize Sensitive Data Exposure

- Avoid placing **sensitive or personal data** in tokens, especially if they may leave secure backends or be stored client-side. :contentReference[oaicite:15]{index=15}

- For sensitive contexts, consider **opaque tokens** on front channels and JWTs only for internal API usage.

---

## ⏳ 8. Token Lifetime and Revocation

- Use **short token lifetimes** (minutes–hours).
- Because JWTs are self-contained, revocation is difficult once issued; short lifetimes limit exposure. :contentReference[oaicite:16]{index=16}

---

## 🚫 9. Do Not Use JWTs for Session Tokens

JWTs are not designed for session tracking; using them as such can impede revocation and lead to insecure logout behaviours. :contentReference[oaicite:17]{index=17}

---

## 📋 10. Auditing and Governance

- Standardize JWT validation across services.
- Perform peer reviews and periodic audits of JWT validation code and configurations. :contentReference[oaicite:18]{index=18}

---

## 11. Prefer Asymmetric Signing Algorithms

### 🔐 11.1 Why Asymmetric Algorithms?

Always prefer **asymmetric signing algorithms** (RSA, ECDSA, EdDSA) over symmetric algorithms (HMAC):

- **Key Separation**: Private key stays on the issuer; public key can be distributed safely to all verifiers.
- **No Shared Secrets**: Eliminates the risk of symmetric key leakage across multiple services.
- **Scalable Trust**: Any service can verify tokens without accessing signing keys.
- **Reduced Attack Surface**: Compromise of a verifier does not compromise token issuance. :contentReference[oaicite:0]{index=0}

### ⚠️ 11.2 Risks of Symmetric Algorithms (HMAC)

Using **HS256/HS384/HS512** requires all parties to share the same secret:

- **Widespread Key Distribution**: Every service that validates must hold the signing key.
- **Increased Exposure**: More copies of the secret = higher chance of compromise.
- **Authority Boundary Collapse**: Any service with the key can forge tokens, not just issue them.

Only use symmetric algorithms when:
- You have a single, tightly controlled validation point
- The secret can be securely managed and rotated
- Performance requirements absolutely demand it (though modern asymmetric crypto is highly efficient)

### ✅ 11.3 Recommended Asymmetric Algorithms

See **Section 3** for detailed algorithm recommendations:

| Algorithm | Use Case |
|-----------|----------|
| **EdDSA (Ed25519)** | Best choice for new systems — fast, secure, and compact |
| **ECDSA (ES256)** | Excellent balance of security and performance |
| **RSA (RS256)** | Widely supported, acceptable for legacy compatibility |

---

## ✅ Summary Checklist

- ☐ Verify **signatures and cryptographic ops**
- ☐ Enforce strict **algorithm allow lists**
- ☐ Validate **issuer, audience, time claims**
- ☐ Distinguish token types and claim usage
- ☐ Use secure **key distribution (JWKS)** and rotate keys
- ☐ Avoid sensitive payload data
- ☐ Enforce short lifetimes and token revocation patterns

---

If you’d like, I can also generate an **example validation library outline** or **code snippet** in your preferred language that implements these best practices.
::contentReference[oaicite:19]{index=19}

# 0003. Secure-by-default authorization

- **Status:** accepted
- **Made by:** Cole, solo refactor (June 2026)
- **Evidence:** commit "Phase A1: secure-by-default global authorization fallback policy" (2026-06-07); the `FallbackPolicy` block in `src/SteamProject/Program.cs`.

## Context

An audit of the capstone code found API controllers (Steam, user, messages, vote) with no `[Authorize]`
attribute at all. Authorization was opt-in per controller, so a forgotten attribute meant an open endpoint.

## Decision

Set a global `FallbackPolicy` that requires an authenticated user. Every endpoint is protected unless it opts
out with `[AllowAnonymous]` (or is mapped with `.AllowAnonymous()`): today that is the landing, privacy, not-found
and error pages, the Identity sign-in pages, the health checks and `/status`. In the same pass, state-changing endpoints got antiforgery validation (the token can arrive in a
`RequestVerificationToken` header for AJAX callers), and API controllers derive the user from the
authenticated identity on the server instead of trusting IDs from the request.

## Consequences

- A new endpoint is closed by default; making something public is a visible, reviewable `[AllowAnonymous]`.
- Anonymous visitors to any locked page were redirected to the login page, including for made-up URLs.
  The public-surface work later added status-code pages so an unknown URL returns a real 404, and `/api`
  requests get 401 or 403 instead of a redirect.

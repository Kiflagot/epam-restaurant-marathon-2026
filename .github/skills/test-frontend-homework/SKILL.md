---
name: test-frontend-homework
description: Run, interpret, and troubleshoot Angular frontend checks in marathon2026. Use when asked to validate components, Owner Group Pre-order workflows, unit tests, builds, or test failures.
---

# Test Frontend Homework

## Scope and Setup
- Work in `frontend/restaurant-frontend-app`.
- Inspect `package.json`, available npm scripts, test configuration, and relevant application tests.
- Read `backend/restaurant-backend-app` only when validating API-contract compatibility; backend changes belong to `DevelopAgent`.
- Use `_frontend-reference` only as a temporary comparison source, not as a required test dependency.
- Preserve Cognito, AWS API, interceptors, routes, and environment configuration during validation.
- Prefer existing dependencies; use `npm ci` only when needed and approved if it changes the environment materially.
- Select available npm scripts from `package.json`; never assume a homework-specific script exists.

## Workflow
1. Determine the requested feature/workflow and select the smallest relevant validation.
2. Run the test or build command, capturing exit status and meaningful failure details.
3. Classify failures: environment/setup, test wiring, TypeScript/build, component behavior, static/mock/API behavior, API-contract mismatch, Cognito, interceptor, route, or environment configuration.
4. Trace failures to source code and requirements; propose minimal corrections. Modify implementation only when asked or already authorized by the task.
5. Rerun the failed check after changes where possible.
6. Report concisely: **Verified** (command and counts), **Issues** (specific failures), **Next** (one recommended action).

## Constraints
- Never alter or delete supplied tests solely to obtain a passing result.
- Do not claim a test passed without execution; distinguish unrun tests from passed tests.
- Avoid changing lockfiles, dependency versions, or test configuration without a justified need.
- Do not run git commit/push or deploy without approval.
- Respond in Russian, with single-line PowerShell commands.

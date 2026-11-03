---
name: implement-angular-component
description: Implement or refine Angular UI components in marathon2026, following existing frontend conventions and API integration boundaries. Use when asked to build a component, template, styling, or component-level behavior.
---

# Implement Angular Component

## Scope
- Work only inside `frontend/restaurant-frontend-app`.
- Inspect the current frontend requirements and implementation; do not require educational files to exist.
- Use `_frontend-reference` only as a temporary reference for already implemented behavior.
- Read `backend/restaurant-backend-app` only to analyze API contracts; backend changes belong to `DevelopAgent`.
- Preserve existing Cognito, AWS API, interceptors, routes, and environment configuration.

## Workflow
1. Locate the existing component, related models, services, styles, routes, and nearby examples. Inspect `package.json` to determine the Angular version and available scripts.
2. Briefly state the smallest change needed. Ask only when requirements or intended behavior are materially ambiguous.
3. Reuse established component patterns and shared utilities; do not create duplicates or assume static/mock data is available.
4. Implement typed inputs/outputs, accessible markup, and responsive styling as required by the task. Keep business logic out of templates where practical.
5. Run the narrowest relevant validation (tests, type checking, or build) that is available. Do not edit provided homework tests to force a pass.
6. Report briefly: **Changed**, **Verified**, **Issues**. State explicitly if tests were not run.

## Constraints
- Support the requested static, mock, or real API workflow without replacing existing integration.
- Preserve Cognito/demo authentication, AWS API configuration, interceptors, and existing routes unless the task explicitly requires a compatible frontend change.
- No new packages or broad refactoring without approval.
- Never commit, push, or deploy without explicit approval.
- Respond in Russian, concisely; give PowerShell commands on one line.

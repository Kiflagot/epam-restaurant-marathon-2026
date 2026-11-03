---
name: implement-group-order
description: Implement the Owner Group Pre-order workflow in marathon2026, including participant review, consolidated dish totals, API states, and submission. Use for tasks concerning group orders or their static, mock, or real API flows.
---

# Implement Owner Group Pre-order

## Sources of Truth
- Existing implementation and requirements in `frontend/restaurant-frontend-app`.
- API contracts inspected read-only in `backend/restaurant-backend-app`.
- `_frontend-reference` only as a temporary source for implemented Practice, Level 1, and Level 2 behavior.
- Existing frontend models, helpers, services, interceptors, routes, environments, and available static/mock infrastructure.

## Workflow
1. Identify the requested workflow and read only the relevant frontend requirements and tests when they exist.
2. Inspect current routes, models, components, services, mocks, and helper implementations before editing.
3. Keep changes limited to the requested workflow:
   - **Static:** Owner review UI and consolidation from available local data; no unnecessary HTTP.
   - **Mock:** Use existing mock API, loading/error/empty states, consolidation, and submission behavior.
   - **Real API:** Inspect backend contracts and integrate existing API/AWS services only when requested; do not remove demo/mock mode by default.
4. Ensure totals and consolidated dish quantities are derived consistently from the required data model; handle missing/empty data according to the applicable frontend requirements.
5. Verify with available npm scripts and relevant application tests; inspect the applicable route when possible.
6. Report **Changed**, **Verified**, **Issues**; disclose unverified behavior.

## Integration Guardrails
- Do not assume real API payload casing matches frontend models; inspect actual contracts before real API integration.
- Backend changes belong to `DevelopAgent`; do not change backend endpoints, Terraform, AWS infrastructure, or unrelated repositories.
- Preserve Cognito, AWS API configuration, interceptors, routes, environments, and mock/demo support.
- Do not change provided tests just to make them pass or require mock data when the selected workflow is static/real.
- No commits, pushes, or deployments without approval.
- Respond briefly in Russian and work one requested task at a time.

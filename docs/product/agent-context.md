# Product context for agents

This is a navigation aid, not a replacement for [SRS_ECMA.md](SRS_ECMA.md). Read the referenced section's full FR/BR tables and SRS §3.13.1 permissions when implementing. M is mandatory; S is Should, not a license to implement unrelated scope.

## Actors and stable rules

Guest browses/searches public events. Registration creates Active Attendee. Admin grants Organizer/Staff roles; Organizer assigns Staff to events. Each user has one role; Admin is seeded and at least one Active Admin must remain. Organizer operations are owner-scoped, Staff operations assignment-scoped. Role changes take effect next request; disabling invalidates existing sessions immediately.

Username is the identity, not email. No email, notifications, real payment, password recovery, 2FA/social login, QR scanning, waitlists, discounts, standalone speaker/room/track/session entities, mobile app, or mobile-responsive requirement. Inline operation feedback is explicitly permitted.

Conference is an Event with the fixed Conference tag, not a separate entity/workflow. Seed the 13 SRS §3.4 tags; events have 1–5 distinct tags. Location/speaker/contact details live in description.

Store instants in UTC; show Asia/Ho_Chi_Minh. Prices are integer VND. Event/order/ticket transitions, cancellation/refunds, concurrent inventory, and ownership require server enforcement. Ticket ownership belongs to the buyer, not separately entered attendees. Pending orders hold inventory 10 minutes; Valid/Used plus Pending tickets count toward the 10-ticket per-user/event limit. Multi-step order operations are transactional and minute jobs idempotent.

## Requirement map and dependencies

| SRS section / ID family | Area | Dependencies to inspect |
|---|---|---|
| §3.1 `FR-AUTH-01..06` | Registration/login/logout/password/session | User persistence, hashing/session decision; ROLE status semantics |
| §3.2 `FR-PROF-01..05` | Profile/avatar/order history | AUTH; REG for ticket/order history |
| §3.3 `FR-EVT-01..11` | Event lifecycle/public details | AUTH/ROLE, CONF tags; TKT before publish; SCH/TKT validation on edits; REG refunds on cancel; timed completion |
| §3.4 `FR-CONF-01..04` | Fixed tags/Conference badge | Tag seed, EVT; SRCH shortcut |
| §3.5 `FR-SRCH-01..08` | Search/filter/sort/paging | EVT visibility/status, CONF, TKT pricing/stock |
| §3.6 `FR-REG-01..09` | Orders/mock payment/holds/tickets/cancel | AUTH/ROLE, EVT, TKT, atomic stock handling, expiry jobs |
| §3.7 `FR-PART-01..07` | Participants/manual issue/cancel/CSV | ROLE ownership, REG/TKT, ATT status |
| §3.8 `FR-SCH-01..06` | Timeline/personal schedule | EVT bounds/status, ROLE ownership, REG tickets |
| §3.9 `FR-TKT-01..06` | Ticket types/codes/stock/revenue | EVT capacity/time, ROLE ownership, REG purchase/holds |
| §3.10 `FR-ATT-01..06` | Check-in/undo/statistics | ROLE assignments, EVT window/status, REG/TKT state |
| §3.11 `FR-DASH-01..04` | Role dashboards | Implemented event/ticket/check-in data; consistent RPT calculations |
| §3.12 `FR-RPT-01..06` | Reports/CSV | ROLE, EVT/CONF, captured prices, REG refunds, ATT |
| §3.13 `FR-ROLE-01..05` | Users/roles/status/Staff assignment | AUTH sessions, EVT published ownership restrictions |

Dependencies do not require implementing whole modules upfront: include only prerequisites needed for the requested slice, with an explicit plan. The SRS has no standalone `FR-05`; multiple modules have a requirement numbered 05.

Conceptual entities (SRS §6): User, Event, Tag, EventTag, TimelineItem, TicketType, Order, OrderItem, Ticket, Checkin, EventStaff. None are currently implemented. Do not duplicate the detailed schema here.

## Conflicts and unresolved decisions

Resolve only questions that block the requested feature; record significant accepted answers in `docs/decisions/`. Do not guess:

1. **Architecture conflict, resolved for this setup:** SRS §2.1 diagrams Frontend/REST API; §4.3 suggests API endpoints and several security clauses say API. The explicit brief requires SSR MVC, which the existing project uses. [Decision 0001](../decisions/0001-server-rendered-mvc.md) adopts MVC/Razor; whether the course separately requires JSON API tests/endpoints still needs user confirmation before any API work.
2. **Authentication choice, resolved:** The user selected cookie authentication and database-stored `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` hashes. [Decision 0002](../decisions/0002-cookie-authentication-password-hashing.md) explicitly overrides BR-AUTH-08's bcrypt/argon2 wording with Identity PBKDF2. Login/registration redirect, Admin seed credential provisioning, and the implementation of immediate status/role invalidation and exact idle timeout remain to be settled. No email/reset flow is allowed.
3. **Event cancellation with Used tickets:** BR-EVT-08 says cancel Valid tickets and Orders/refund; BR-TKT-08 disallows Used state changes except undo and BR-PART-03 disallows cancelling Used. Decide the Order/refund outcome when an event with check-ins is cancelled.
4. **Organizer cancellation policy:** FR-PART-06 permits participant cancellation with reason but does not say whether the Attendee ≥24-hour rule applies. Clarify time/status limits for privileged cancellation.
5. **Manual free ticket against priced type:** FR-PART-05 grants a free ticket while BR-PART-06 consumes selected TicketType stock. Decide allowable types, zero-price Order/OrderItem representation, and enforcement of per-user/event limits and sale/event windows.
6. **Sold/held accounting:** Define whether “has sold tickets” includes subsequently cancelled/refunded tickets, how Pending holds affect quantity reductions/unpublish/type deletion, and whether `TicketType.sold` is historical or current. Capacity and race safety still apply.
7. **UI language and labels:** SRS permits English or Vietnamese, consistently; current template is English. Choose before shipping product UI. Gender values and username/password Unicode/whitespace interpretation beyond the explicit rules also need definition where relevant.
8. **Permissions presentation:** §4.1 labels some screens Attendee/Organizer while §3.13.1 gives broader Admin/Staff rights; §3.7 heading omits Staff read-only access present in the matrix. Do not silently deny matrix permissions or infer new ones; resolve contradictions when implementing those screens.
9. **Reporting/time edges:** Which timestamp date filters and sales-by-day groupings use, cancelled-ticket inclusion in “sold,” exact sale/check-in/hold-expiry endpoint inclusivity, and date-range timezone interpretation are not fully specified. Use explicit rules where given (≥24-hour cancellation, timeline touching allowed); obtain decisions for unspecified edges.
10. **Check-in history:** Undo must be recorded (BR-ATT-06), but the suggested Checkin schema only provides `is_undone`, not undo actor/time. Decide minimal history fields and repeat check-in representation. Operational/check-in records do not imply the excluded general audit-log feature.

Performance requirements (NFR-01/02), seed/reset (NFR-12), and controllable time (NFR-13) need verifiable checks as their supporting features are built; this documentation setup does not implement them.

Frontend technology is settled: Razor `.cshtml` plus Tailwind CSS v4 per [decision 0003](../decisions/0003-razor-tailwind-styling.md). UI language remains unresolved. Tailwind dependencies are installed, but the template still renders Bootstrap and has no Tailwind compilation pipeline.
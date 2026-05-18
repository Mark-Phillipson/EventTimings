## Plan: Mobile Event Timing App

The recommended shape is a Blazor PWA that opens on phones, backed by an ASP.NET Core API and a centralized Azure SQL Database. That gives you one cloud source of truth for the event, while still letting officials keep working through short connectivity drops by queueing actions locally and syncing them back when signal returns.

**Steps**

1. Define the event workflow and data model first: event, rider, official, timing session, start record, stop record, and audit trail.
2. Build the app as a phone-first Blazor PWA with a separate backend API so the database stays authoritative and the UI stays fast on mobile.
3. Implement simple official identity with name plus PIN, and bind every timing action to the signed-in official for traceability.
4. Add offline resilience by storing pending timing actions locally on the phone and replaying them when connectivity returns.
5. Host the backend on Azure App Service or Azure Container Apps, use Azure SQL Database for the shared event data, and add Application Insights for monitoring.
6. Design the field UI around large touch targets, a clear event status banner, a running timer view, and very few taps per action.
7. Add an admin area for event setup, rider management, official assignment, and post-event export/reporting.
8. Validate the full flow with phone testing, offline sync testing, Azure deployment testing, and a short pilot before event day.

**Decisions**

- I assumed brief offline periods must be tolerated.
- I assumed officials will sign in with name plus PIN rather than Microsoft login.
- I assumed the first release should cover timing plus rider/event management, not the full race-management stack.
- I assumed a PWA is the right delivery format so the same app works in a browser and can be installed on phones.
- I assumed Azure SQL Database is the right centralized store for this event-focused workload.

If you want, the next useful step is to turn this into a concrete project blueprint with suggested solution structure, Azure resources, and the first backlog of user stories.
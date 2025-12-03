# Open Order System (OOS) Core

**OOS Core** is the central application for the Open Order System — powering order management, administrative tooling, device communication, and the backend APIs used throughout the OOS ecosystem.

Unlike a traditional “pure backend,” OOS Core currently includes:

- A full **Staff Interface** for order processing and live ticket management  
- A **Manager/Admin Interface** for products, categories, ingredients, pricing, and configuration  
- A fully functional **headless backend API layer**, suitable for building custom ordering frontends  
- No built-in customer-facing order site yet, but the API provides all necessary endpoints to create one  
- **Over 3,700 customer orders processed and counting!**

> **Note:** A customizable, built-in customer ordering interface is planned for a future update.  
> This interface will be part of OOS Core itself and will allow Core to operate in two modes:
>
> - **Headless Mode** — using only the API with an external frontend (kiosks, custom sites, OOSRuntime, etc.)  
> - **Integrated Mode** — using Core as an “all-in-one” solution with staff tools, admin tools, and a customer-facing site in one application
>
> The goal is to maintain full headless capability while still offering a complete turnkey solution for deployments that prefer a unified platform.

OOS Core is actively evolving following a major architectural split.  
It is currently in a **pre-1.0.0 development phase**: the system is production-ready, but internal structures and interfaces are continuing to be refined as we move toward a stable 1.0 release.

---

## ✨ Features

### 🟦 Core System
- API-first architecture with structured, versioned endpoints (v1 in development)
- Local receipt printer support (ESC/POS compatible) via the **OOS PrintBridge** companion application
- Daily sales reports built in (with expanded reporting tools in development)
- Extensible order pipeline with real-time state updates
- Headless-friendly design suitable for custom frontends, kiosks, and external integrations

### 🟩 Staff-Facing Tools (Included in Core)
- Web-based staff terminal for viewing, advancing, and completing orders
- Real-time order status updates (via backend API)
- Device-friendly interface designed for touchscreens and kiosk environments

### 🟧 Administrative Tools (Included in Core)
- Product, category, ingredient, and pricing management
- Promotion and discount schema management
- Reporting, diagnostics, and system health tools
- User and role management (via ASP.NET Core Identity)

---

## 🛣 Roadmap (High-Level)
- Finalize **Core ↔ Frontend** architectural separation  
- Introduce a stable **v1 API** with clearly defined public boundaries  
- Improve staff/admin interface UX and modularity  
- Complete setup wizard, recovery key system, and configuration workflow  
- Add a **built-in, themeable public-facing ordering interface**  
- Harden and expand the device ecosystem (PrintBridge, Quick BOOP, kiosk modules)  
- Add internal event bus views and diagnostics  
- Continue building automated migration and recovery utilities  

---

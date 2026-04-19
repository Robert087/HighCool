# ERP System – Architecture & Offline Strategy (v1)

---

## 1. System Overview

This system is designed as a:

* **Responsive Web Application**
* Accessible via:

  * Desktop browsers
  * Mobile browsers
* Future-ready to be installed as a **Progressive Web App (PWA)**

### Core Principle

> One system, one backend, one source of truth.

---

## 2. High-Level Architecture

### Backend

* Centralized API (ASP.NET Core / Node.js)
* Relational database (SQL Server / PostgreSQL)
* Responsible for:

  * Business logic
  * Data integrity
  * Posting workflows
  * Validation rules

---

### Frontend

* Responsive Web UI
* Single codebase for all devices
* No separate mobile application required
* Communicates with backend via API

---

## 3. Source of Truth

> The **server is always the source of truth**.

### Rules

* All official data is stored on the backend
* Stock, balances, and financial data are **never finalized locally**
* Local storage is used only as a **temporary buffer**
* All business decisions must be confirmed by the server

---

## 4. Offline Strategy

### Design Philosophy

> Keep offline simple, predictable, and safe.

This system does **NOT** implement full offline-first architecture.

Instead:

* Offline is supported only for **draft creation**
* All critical operations require **server confirmation**

---

## 5. Offline Behavior

### When Internet is Available

Users can:

* Create documents
* Edit documents
* Post documents
* Perform allocations
* Execute all business operations

---

### When Internet is NOT Available

Users can ONLY:

* Create drafts
* Edit drafts

Users CANNOT:

* Post documents
* Affect stock
* Affect balances
* Perform allocations
* Execute financial or inventory logic

---

## 6. Local Storage Strategy

### Technology

* **IndexedDB (browser-based storage)**

### Stored Data

* Draft documents only
* Pending unsent operations

### Important Rules

* Data is temporary
* Data is tied to device/browser
* Data is not guaranteed permanent
* Data must be synced to backend as soon as possible

---

## 7. Draft Lifecycle

### 7.1 Offline Draft Creation

When offline:

* Draft is created locally
* Stored in IndexedDB
* Marked as:

```text
local_only = true
sync_status = pending
```

---

### 7.2 When Internet is Restored

System behavior:

* Detect connection automatically
* Notify user:

> "You have pending drafts"

User actions:

* Review drafts
* Submit manually

---

### 7.3 Draft Submission

On submit:

* Draft is sent to backend
* Backend performs validation
* Backend processes business logic

If successful:

* Data is stored permanently in database
* Local draft is:

  * marked as synced
  * or removed

---

## 8. Posting Strategy

> All posting is server-side only.

### Rules

* No posting logic runs offline
* No stock updates happen locally
* No financial impact is recorded locally

All posting must:

* Go through backend services
* Pass validation rules
* Be idempotent (safe from duplication)

---

## 9. User Experience Rules

### When Offline

System must:

* Show clear message:

  > "No internet – saved locally as draft"
* Allow continued draft entry
* Disable posting actions

---

### When Internet Returns

System must:

* Notify user of pending drafts
* Provide action:

  * **Submit Pending Drafts**

---

## 10. Restart Behavior

* Drafts stored in IndexedDB will **usually survive device restart**

### Limitations

* Clearing browser data will delete drafts
* Switching devices will not carry drafts

### Rule

> Local drafts are temporary, not guaranteed storage.

---

## 11. Explicitly Out of Scope (For Simplicity)

To keep the system simple and stable, we do NOT implement:

* Full offline posting
* Real-time synchronization engines
* Conflict resolution engines
* Multi-device offline merging
* Full local database mirroring

---

## 12. Design Summary

| Aspect           | Decision                |
| ---------------- | ----------------------- |
| Platform         | Web Application         |
| Mobile Support   | Responsive UI           |
| Offline Mode     | Draft-only              |
| Storage          | IndexedDB               |
| Posting          | Server-side only        |
| Sync             | Manual (user-triggered) |
| Complexity Level | Minimal                 |

---

## 13. Guiding Principle

> Offline is for **continuity**, not **authority**.

* Users can continue working during connection loss
* System integrity is preserved by server-side control

---

## 14. Future Extensions (Optional)

This architecture allows safe future upgrades:

* Automatic sync
* Background sync
* Partial offline modules

Without breaking the current system design

---

# End of Document

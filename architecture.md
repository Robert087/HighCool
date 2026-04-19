# ERP System – Architecture & Offline Strategy (v1)

## 1. System Type

This system is designed as a:

* **Web Application (Responsive)**
* Accessible from:

  * Desktop browsers
  * Mobile browsers
* Future-ready to be installed as a **Progressive Web App (PWA)**

### Key Principle

> One system, one backend, one source of truth.

---

## 2. Core Architecture

### Backend

* Centralized API (e.g. ASP.NET Core / Node.js)
* Relational Database (SQL Server / PostgreSQL)

### Frontend

* Responsive Web UI
* Works across all screen sizes
* No separate mobile app required

---

## 3. Source of Truth

> The **server is always the source of truth**.

* All official data lives on the backend
* Stock, balances, statements, and financial logic are **never finalized locally**
* Local storage is used only as a temporary buffer

---

## 4. Offline Strategy (Simplified)

### Design Philosophy

> Keep offline simple, predictable, and safe.

We do **NOT** implement full offline-first architecture.

Instead:

* Offline is supported only for **draft creation**
* All critical operations require server confirmation

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

* Create Drafts
* Edit Drafts

Users CANNOT:

* Post documents
* Affect stock
* Affect balances
* Perform allocations
* Execute financial or inventory logic

---

## 6. Local Storage Mechanism

### Technology

* **IndexedDB (Browser Local Storage)**

### Stored Data

* Draft documents only
* Pending unsent operations

### Important Rules

* Local data is temporary
* It is tied to the device/browser
* It is not guaranteed permanent storage

---

## 7. Draft Lifecycle

### Offline Draft

* Created when no internet
* Stored locally
* Marked as:

  * `local_only = true`
  * `sync_status = pending`

---

### After Internet Restores

System behavior:

* Detects connection
* Shows:
  **"You have pending drafts"**

User actions:

* Review drafts
* Submit manually

---

### On Submit

* Draft is sent to backend
* Backend validates and processes
* If successful:

  * Stored permanently in database
  * Local draft marked as synced or removed

---

## 8. Posting Rules

> Posting is always server-side.

* No posting logic runs offline
* No stock updates happen locally
* No financial impact is recorded locally

All posting must:

* Go through backend services
* Follow validation rules
* Be idempotent

---

## 9. User Experience Rules

### When Offline

System must:

* Show clear message:

  * "No internet – saved locally as draft"
* Allow continued draft entry
* Prevent posting actions

---

### When Online Returns

System must:

* Notify user of pending drafts
* Provide action:

  * "Submit Pending Drafts"

---

## 10. Restart Behavior

* Drafts stored in IndexedDB will **usually survive device restart**
* However:

  * Clearing browser data will remove them
  * Switching devices will not carry drafts

### Rule

> Local drafts are temporary, not guaranteed storage.

---

## 11. What We Explicitly Avoid

To keep the system simple and stable, we do NOT implement:

* Full offline posting
* Real-time sync engines
* Conflict resolution engines
* Multi-device offline merge
* Local database mirroring full system

---

## 12. Design Summary

| Aspect           | Decision                |
| ---------------- | ----------------------- |
| Platform         | Web App                 |
| Mobile Support   | Responsive UI           |
| Offline Mode     | Draft-only              |
| Storage          | IndexedDB               |
| Posting          | Server-side only        |
| Sync             | Manual (user-triggered) |
| Complexity Level | Minimal                 |

---

## 13. Guiding Principle

> Offline is for **continuity**, not **authority**.

* Users can continue working
* But system integrity is preserved by server-side control

---

## 14. Future Extension (Optional)

This design allows future upgrades:

* Auto-sync
* Background sync
* Partial offline modules

Without breaking current architecture

---

# End of Document
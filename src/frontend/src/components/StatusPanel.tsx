import { useEffect, useState } from "react";

export function StatusPanel() {
  const [isOnline, setIsOnline] = useState(() => navigator.onLine);

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  return (
    <section className="status-panel">
      <div>
        <span className={`status-dot ${isOnline ? "online" : "offline"}`} />
        <strong>{isOnline ? "Online" : "Offline"}</strong>
      </div>
      <p>Offline support is reserved for draft work only.</p>
      <p>Pending drafts: 0</p>
    </section>
  );
}

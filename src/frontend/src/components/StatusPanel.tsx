import { useEffect, useState } from "react";
import { Badge, Card } from "./ui";

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
    <Card className="status-panel hc-status-panel" muted padding="md">
      <div className="hc-status-panel__status">
        <span className={`status-dot ${isOnline ? "online" : "offline"}`} />
        <div className="hc-status-panel__copy">
          <p className="hc-status-panel__title">{isOnline ? "Workspace online" : "Workspace offline"}</p>
          <p className="hc-status-panel__description">Offline support is reserved for draft work only.</p>
        </div>
      </div>
      <div className="hc-status-panel__meta">
        <Badge tone={isOnline ? "success" : "warning"}>{isOnline ? "Connected" : "Offline"}</Badge>
        <Badge tone="neutral">Pending drafts: 0</Badge>
      </div>
    </Card>
  );
}

import { useEffect, useState, type PropsWithChildren } from "react";
import { Button } from "../components/ui";
import { useI18n } from "../i18n";
import {
  isDesktopRuntime,
  resetDesktopRuntimeInfo,
  resolveDesktopRuntimeInfo,
  verifyDesktopBackendHealth,
  type DesktopRuntimeInfo,
} from "../services/apiRuntime";

type RuntimeStatus = "ready" | "resolving" | "connecting" | "unavailable";

export function DesktopRuntimeGate({ children }: PropsWithChildren) {
  const { t } = useI18n();
  const [status, setStatus] = useState<RuntimeStatus>(() => isDesktopRuntime() ? "resolving" : "ready");
  const [runtimeInfo, setRuntimeInfo] = useState<DesktopRuntimeInfo | null>(null);
  const [error, setError] = useState("");

  async function resolveRuntime() {
    if (!isDesktopRuntime()) {
      setStatus("ready");
      return;
    }

    try {
      setError("");
      setStatus("resolving");
      const info = await resolveDesktopRuntimeInfo();
      if (!info) {
        setStatus("ready");
        return;
      }

      setRuntimeInfo(info);
      setStatus("connecting");
      await verifyDesktopBackendHealth(info);
      setStatus("ready");
    } catch (runtimeError) {
      setError(runtimeError instanceof Error ? runtimeError.message : t("desktopRuntime.errorUnknown"));
      setStatus("unavailable");
    }
  }

  useEffect(() => {
    void resolveRuntime();
  }, []);

  if (status === "ready") {
    return <>{children}</>;
  }

  return (
    <main className="hc-runtime-state" role="status" aria-live="polite">
      <div className="hc-runtime-state__panel">
        <p className="hc-runtime-state__eyebrow">{t("desktopRuntime.eyebrow")}</p>
        <h1>{t(status === "unavailable" ? "desktopRuntime.unavailableTitle" : "desktopRuntime.startingTitle")}</h1>
        <p>
          {status === "resolving"
            ? t("desktopRuntime.resolving")
            : status === "connecting"
              ? t("desktopRuntime.connecting")
              : t("desktopRuntime.unavailableDescription")}
        </p>
        {runtimeInfo ? (
          <dl className="hc-runtime-state__diagnostics">
            <div>
              <dt>{t("desktopRuntime.backendOrigin")}</dt>
              <dd>{runtimeInfo.apiOrigin}</dd>
            </div>
          </dl>
        ) : null}
        {error ? <p className="hc-runtime-state__error">{error}</p> : null}
        {status === "unavailable" ? (
          <Button
            variant="secondary"
            onClick={() => {
              resetDesktopRuntimeInfo();
              void resolveRuntime();
            }}
          >
            common.retry
          </Button>
        ) : null}
      </div>
    </main>
  );
}

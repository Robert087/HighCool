import type { PropsWithChildren, ReactNode } from "react";
import { PageHeader } from "../ui";

interface FormPageLayoutProps {
  eyebrow?: string;
  title?: string;
  description?: string;
  actions?: ReactNode;
  width?: "default" | "wide";
}

export function FormPageLayout({
  actions,
  children,
  description,
  eyebrow,
  title,
  width = "default",
}: PropsWithChildren<FormPageLayoutProps>) {
  const hasHeader = Boolean(eyebrow || title || description || actions);

  return (
    <section className="hc-form-page">
      {hasHeader ? (
        <div className="hc-form-page__header">
          <PageHeader eyebrow={eyebrow} title={title ?? ""} description={description} actions={actions} />
        </div>
      ) : null}
      <div className={`hc-form-stack${width === "wide" ? " hc-form-stack--wide" : ""}`}>{children}</div>
    </section>
  );
}

import type { PropsWithChildren, ReactNode } from "react";
import { PageHeader } from "../ui";

interface FormPageLayoutProps {
  eyebrow: string;
  title: string;
  description: string;
  actions?: ReactNode;
}

export function FormPageLayout({
  actions,
  children,
  description,
  eyebrow,
  title,
}: PropsWithChildren<FormPageLayoutProps>) {
  return (
    <section className="hc-form-page">
      <div className="hc-form-page__header">
        <PageHeader eyebrow={eyebrow} title={title} description={description} actions={actions} />
      </div>
      <div className="hc-form-stack">{children}</div>
    </section>
  );
}

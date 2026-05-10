import type { HTMLAttributes, PropsWithChildren, ReactNode } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

interface DocumentSectionProps extends HTMLAttributes<HTMLElement> {
  title: string;
  description?: string;
  actions?: ReactNode;
}

export function DocumentSection({
  actions,
  children,
  className,
  description,
  title,
  ...props
}: PropsWithChildren<DocumentSectionProps>) {
  const { translateText } = useI18n();

  return (
    <section className={cn("hc-document-section", className)} {...props}>
      <div className="hc-document-section__header">
        <div className="hc-document-section__copy">
          <h2 className="hc-document-section__title">{translateText(title)}</h2>
          {description ? <p className="hc-document-section__description">{translateText(description)}</p> : null}
        </div>
        {actions ? <div className="hc-document-section__actions">{localizeReactNode(actions, translateText)}</div> : null}
      </div>
      <div className="hc-document-section__content">{children}</div>
    </section>
  );
}

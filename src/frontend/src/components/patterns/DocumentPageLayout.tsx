import type { PropsWithChildren, ReactNode } from "react";
import { localizeReactNode, useI18n } from "../../i18n";

interface DocumentPageLayoutProps {
  title: string;
  eyebrow?: string;
  description?: string;
  status?: ReactNode;
  actions?: ReactNode;
  footer?: ReactNode;
}

export function DocumentPageLayout({
  actions,
  children,
  description,
  eyebrow,
  footer,
  status,
  title,
}: PropsWithChildren<DocumentPageLayoutProps>) {
  const { translateText } = useI18n();

  return (
    <section className="hc-document-page">
      <div className="hc-document-page__surface">
        <header className="hc-document-page__header">
          <div className="hc-document-page__header-main">
            {eyebrow ? <p className="hc-document-page__eyebrow">{translateText(eyebrow)}</p> : null}
            <div className="hc-document-page__headline">
              <h1 className="hc-document-page__title">{translateText(title)}</h1>
              {status ? <div className="hc-document-page__status">{localizeReactNode(status, translateText)}</div> : null}
            </div>
            {description ? <p className="hc-document-page__description">{translateText(description)}</p> : null}
          </div>
          {actions ? <div className="hc-document-page__actions">{localizeReactNode(actions, translateText)}</div> : null}
        </header>

        <div className="hc-document-page__body">{children}</div>

        {footer ? <footer className="hc-document-page__footer">{localizeReactNode(footer, translateText)}</footer> : null}
      </div>
    </section>
  );
}

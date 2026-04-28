import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

export interface PageHeaderProps extends HTMLAttributes<HTMLElement> {
  title: string;
  description?: string;
  eyebrow?: string;
  actions?: ReactNode;
}

export function PageHeader({
  actions,
  className,
  description,
  eyebrow,
  title,
  ...props
}: PageHeaderProps) {
  const { translateText } = useI18n();

  return (
    <header className={cn("hc-page-header", className)} {...props}>
      <div className="hc-page-header__copy">
        {eyebrow ? <p className="hc-page-header__eyebrow">{translateText(eyebrow)}</p> : null}
        <h1 className="hc-page-header__title">{translateText(title)}</h1>
        {description ? <p className="hc-page-header__description">{translateText(description)}</p> : null}
      </div>
      {actions ? <div className="hc-page-header__actions">{localizeReactNode(actions, translateText)}</div> : null}
    </header>
  );
}

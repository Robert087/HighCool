import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";

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
  return (
    <header className={cn("hc-page-header", className)} {...props}>
      <div className="hc-page-header__copy">
        {eyebrow ? <p className="hc-page-header__eyebrow">{eyebrow}</p> : null}
        <h1 className="hc-page-header__title">{title}</h1>
        {description ? <p className="hc-page-header__description">{description}</p> : null}
      </div>
      {actions ? <div className="hc-page-header__actions">{actions}</div> : null}
    </header>
  );
}

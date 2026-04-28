import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

export interface EmptyStateProps extends HTMLAttributes<HTMLDivElement> {
  title: string;
  description: string;
  action?: ReactNode;
  centered?: boolean;
}

export function EmptyState({
  action,
  centered = false,
  className,
  description,
  title,
  ...props
}: EmptyStateProps) {
  const { translateText } = useI18n();

  return (
    <div
      className={cn("hc-empty-state", centered && "hc-empty-state--centered", className)}
      {...props}
    >
      <h2 className="hc-empty-state__title">{translateText(title)}</h2>
      <p className="hc-empty-state__description">{translateText(description)}</p>
      {action ? <div className="hc-empty-state__action">{localizeReactNode(action as ReactNode, translateText)}</div> : null}
    </div>
  );
}

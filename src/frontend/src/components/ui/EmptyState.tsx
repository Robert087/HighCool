import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";

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
  return (
    <div
      className={cn("hc-empty-state", centered && "hc-empty-state--centered", className)}
      {...props}
    >
      <h2 className="hc-empty-state__title">{title}</h2>
      <p className="hc-empty-state__description">{description}</p>
      {action ? <div className="hc-empty-state__action">{action}</div> : null}
    </div>
  );
}

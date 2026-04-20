import type { HTMLAttributes, PropsWithChildren } from "react";
import { cn } from "../../lib/cn";

type CardPadding = "sm" | "md" | "lg";

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  padding?: CardPadding;
  muted?: boolean;
  elevated?: boolean;
}

export function Card({
  children,
  className,
  elevated = false,
  muted = false,
  padding = "md",
  ...props
}: PropsWithChildren<CardProps>) {
  return (
    <div
      className={cn(
        "hc-card",
        `hc-card--${padding}`,
        muted && "hc-card--muted",
        elevated && "hc-card--elevated",
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}

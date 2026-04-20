import type { HTMLAttributes, PropsWithChildren } from "react";
import { cn } from "../../lib/cn";

type ContainerWidth = "wide" | "narrow";

export interface PageContainerProps extends HTMLAttributes<HTMLDivElement> {
  width?: ContainerWidth;
}

export function PageContainer({
  children,
  className,
  width = "wide",
  ...props
}: PropsWithChildren<PageContainerProps>) {
  return (
    <div className={cn("hc-page-container", `hc-page-container--${width}`, className)} {...props}>
      {children}
    </div>
  );
}

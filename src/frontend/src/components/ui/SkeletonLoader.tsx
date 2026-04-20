import type { CSSProperties, HTMLAttributes } from "react";
import { cn } from "../../lib/cn";

type SkeletonVariant = "line" | "rect" | "circle";

export interface SkeletonLoaderProps extends HTMLAttributes<HTMLDivElement> {
  variant?: SkeletonVariant;
  width?: CSSProperties["width"];
  height?: CSSProperties["height"];
}

export function SkeletonLoader({
  className,
  height,
  style,
  variant = "line",
  width,
  ...props
}: SkeletonLoaderProps) {
  return (
    <div
      className={cn("hc-skeleton", `hc-skeleton--${variant}`, className)}
      style={{ ...style, width, height }}
      {...props}
    />
  );
}

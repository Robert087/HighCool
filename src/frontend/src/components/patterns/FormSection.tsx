import type { PropsWithChildren } from "react";
import { Card } from "../ui";

interface FormSectionProps {
  title: string;
  description?: string;
}

export function FormSection({
  children,
  description,
  title,
}: PropsWithChildren<FormSectionProps>) {
  return (
    <Card className="hc-form-section" padding="md">
      <div className="hc-form-section__header">
        <h3 className="hc-form-section__title">{title}</h3>
        {description ? <p className="hc-form-section__description">{description}</p> : null}
      </div>
      <div className="hc-form-section__content">{children}</div>
    </Card>
  );
}

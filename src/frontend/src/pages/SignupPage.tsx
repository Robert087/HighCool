import { useState } from "react";
import { Link, Navigate } from "react-router-dom";
import { Button, Card, Field, Input, PageContainer } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { useI18n } from "../i18n";

export function SignupPage() {
  const { t } = useI18n();
  const { error, isAuthenticated, signup } = useAuth();
  const [fullName, setFullName] = useState("");
  const [organizationName, setOrganizationName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (isAuthenticated) {
    return <Navigate to="/workspace" replace />;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);

    try {
      await signup({ fullName, organizationName, email, password });
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <PageContainer>
      <div className="auth-page">
        <Card className="auth-card">
          <form className="auth-form" onSubmit={handleSubmit}>
            <div className="auth-card__header">
              <p className="auth-card__eyebrow">{t("auth.signup.eyebrow")}</p>
              <h1>{t("auth.signup.title")}</h1>
              <p>{t("auth.signup.description")}</p>
            </div>

            <Field label={t("auth.fullName")}>
              <Input value={fullName} onChange={(event) => setFullName(event.target.value)} />
            </Field>

            <Field label={t("auth.organizationName")}>
              <Input value={organizationName} onChange={(event) => setOrganizationName(event.target.value)} />
            </Field>

            <Field label={t("auth.email")}>
              <Input value={email} onChange={(event) => setEmail(event.target.value)} />
            </Field>

            <Field label={t("auth.password")}>
              <Input type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
            </Field>

            {error ? <p className="auth-card__error">{error}</p> : null}

            <Button disabled={isSubmitting} type="submit">
              {isSubmitting ? t("auth.creatingAccount") : t("auth.signup.submit")}
            </Button>

            <p className="auth-card__footer">
              {t("auth.signup.hasAccount")} <Link to="/login">{t("auth.signup.signIn")}</Link>
            </p>
          </form>
        </Card>
      </div>
    </PageContainer>
  );
}

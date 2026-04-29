import { useState } from "react";
import { Navigate, Link } from "react-router-dom";
import { Button, Card, Field, Input, PageContainer } from "../components/ui";
import { useI18n } from "../i18n";
import { useAuth } from "../features/auth/AuthProvider";

export function LoginPage() {
  const { t } = useI18n();
  const { error, isAuthenticated, login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (isAuthenticated) {
    return <Navigate to="/workspace" replace />;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);

    try {
      await login({ email, password, rememberMe });
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
              <p className="auth-card__eyebrow">{t("auth.login.eyebrow")}</p>
              <h1>{t("auth.login.title")}</h1>
              <p>{t("auth.login.description")}</p>
            </div>

            <Field label={t("auth.email")}>
              <Input value={email} onChange={(event) => setEmail(event.target.value)} />
            </Field>

            <Field label={t("auth.password")}>
              <Input type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
            </Field>

            <label className="auth-card__checkbox">
              <input checked={rememberMe} type="checkbox" onChange={(event) => setRememberMe(event.target.checked)} />
              <span>{t("auth.rememberMe")}</span>
            </label>

            {error ? <p className="auth-card__error">{error}</p> : null}

            <Button disabled={isSubmitting} type="submit">
              {isSubmitting ? t("auth.signingIn") : t("auth.login.submit")}
            </Button>

            <p className="auth-card__footer">
              {t("auth.login.noAccount")} <Link to="/signup">{t("auth.login.createAccount")}</Link>
            </p>
          </form>
        </Card>
      </div>
    </PageContainer>
  );
}

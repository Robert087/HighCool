import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <section className="card">
      <p className="eyebrow">404</p>
      <h2>Page not found</h2>
      <p>The requested route is not part of the initial scaffold.</p>
      <Link className="text-link" to="/">
        Return to dashboard
      </Link>
    </section>
  );
}

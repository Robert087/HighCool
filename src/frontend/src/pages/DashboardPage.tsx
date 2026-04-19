const cards = [
  {
    title: "Suppliers",
    description: "Create, edit, search, and deactivate supplier records ready for future statement linkage.",
  },
  {
    title: "Warehouses",
    description: "Manage active warehouse records before stock-ledger-backed inventory flows are introduced.",
  },
  {
    title: "UOMs",
    description: "Maintain the initial unit-of-measure catalogue with precision and fraction rules.",
  },
];

export function DashboardPage() {
  return (
    <section>
      <div className="hero">
        <p className="eyebrow">Phase 1</p>
        <h2>Master data delivery has started</h2>
        <p className="hero-copy">
          The first operational slice now covers suppliers, warehouses, and units of measure,
          while keeping the server as the source of truth.
        </p>
      </div>

      <div className="card-grid">
        {cards.map((card) => (
          <article key={card.title} className="card">
            <h3>{card.title}</h3>
            <p>{card.description}</p>
          </article>
        ))}
      </div>
    </section>
  );
}

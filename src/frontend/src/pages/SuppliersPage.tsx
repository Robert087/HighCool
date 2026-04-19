import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { deactivateSupplier, listSuppliers, type Supplier } from "../services/masterDataApi";

export function SuppliersPage() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listSuppliers(search, status);

        if (active) {
          setSuppliers(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load suppliers.");
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, [search, status]);

  async function handleDeactivate(id: string) {
    try {
      await deactivateSupplier(id);
      setSuppliers((current) =>
        current.map((supplier) =>
          supplier.id === id ? { ...supplier, isActive: false } : supplier,
        ),
      );
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate supplier.");
    }
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>Suppliers</h2>
          <p className="page-copy">Manage supplier identities used for future statements and procurement flows.</p>
        </div>
        <Link className="primary-button" to="/suppliers/new">
          New supplier
        </Link>
      </div>

      <div className="toolbar">
        <input
          className="text-input"
          placeholder="Search by code, name, or statement name"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <select className="select-input" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="all">All</option>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
        </select>
      </div>

      {error ? <p className="feedback error">{error}</p> : null}
      {loading ? <p className="feedback">Loading suppliers...</p> : null}

      {!loading ? (
        <div className="data-table">
          <div className="data-row data-row--head">
            <span>Supplier</span>
            <span>Statement Link</span>
            <span>Contact</span>
            <span>Status</span>
            <span>Actions</span>
          </div>
          {suppliers.map((supplier) => (
            <div key={supplier.id} className="data-row">
              <span>
                <strong>{supplier.name}</strong>
                <small>{supplier.code}</small>
              </span>
              <span>{supplier.statementName}</span>
              <span>{supplier.phone || supplier.email || "No contact"}</span>
              <span className={supplier.isActive ? "status-pill active" : "status-pill inactive"}>
                {supplier.isActive ? "Active" : "Inactive"}
              </span>
              <span className="row-actions">
                <Link className="text-link-dark" to={`/suppliers/${supplier.id}/edit`}>
                  Edit
                </Link>
                {supplier.isActive ? (
                  <button className="secondary-button" onClick={() => void handleDeactivate(supplier.id)} type="button">
                    Deactivate
                  </button>
                ) : null}
              </span>
            </div>
          ))}
          {!suppliers.length ? <p className="feedback">No suppliers found for the current filter.</p> : null}
        </div>
      ) : null}
    </section>
  );
}

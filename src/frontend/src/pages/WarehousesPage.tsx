import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { deactivateWarehouse, listWarehouses, type Warehouse } from "../services/masterDataApi";

export function WarehousesPage() {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
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
        const result = await listWarehouses(search, status);

        if (active) {
          setWarehouses(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load warehouses.");
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
      await deactivateWarehouse(id);
      setWarehouses((current) =>
        current.map((warehouse) =>
          warehouse.id === id ? { ...warehouse, isActive: false } : warehouse,
        ),
      );
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate warehouse.");
    }
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>Warehouses</h2>
          <p className="page-copy">Manage warehouse records before stock-ledger-driven inventory flows are added.</p>
        </div>
        <Link className="primary-button" to="/warehouses/new">
          New warehouse
        </Link>
      </div>

      <div className="toolbar">
        <input
          className="text-input"
          placeholder="Search by code, name, or location"
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
      {loading ? <p className="feedback">Loading warehouses...</p> : null}

      {!loading ? (
        <div className="data-table">
          <div className="data-row data-row--head">
            <span>Warehouse</span>
            <span>Location</span>
            <span>Status</span>
            <span>Actions</span>
          </div>
          {warehouses.map((warehouse) => (
            <div key={warehouse.id} className="data-row">
              <span>
                <strong>{warehouse.name}</strong>
                <small>{warehouse.code}</small>
              </span>
              <span>{warehouse.location || "Not set"}</span>
              <span className={warehouse.isActive ? "status-pill active" : "status-pill inactive"}>
                {warehouse.isActive ? "Active" : "Inactive"}
              </span>
              <span className="row-actions">
                <Link className="text-link-dark" to={`/warehouses/${warehouse.id}/edit`}>
                  Edit
                </Link>
                {warehouse.isActive ? (
                  <button className="secondary-button" onClick={() => void handleDeactivate(warehouse.id)} type="button">
                    Deactivate
                  </button>
                ) : null}
              </span>
            </div>
          ))}
          {!warehouses.length ? <p className="feedback">No warehouses found for the current filter.</p> : null}
        </div>
      ) : null}
    </section>
  );
}

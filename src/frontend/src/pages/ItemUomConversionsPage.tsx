import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import {
  deactivateItemUomConversion,
  listItemUomConversions,
  type ItemUomConversion,
} from "../services/masterDataApi";

export function ItemUomConversionsPage() {
  const [rows, setRows] = useState<ItemUomConversion[]>([]);
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
        const result = await listItemUomConversions(search, status);
        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load item UOM conversions.");
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
      await deactivateItemUomConversion(id);
      setRows((current) => current.map((row) => (row.id === id ? { ...row, isActive: false } : row)));
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate conversion.");
    }
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>Item UOM Conversions</h2>
          <p className="page-copy">Define per-item conversion factors and rounding settings between UOM pairs.</p>
        </div>
        <Link className="primary-button" to="/item-uom-conversions/new">
          New conversion
        </Link>
      </div>

      <div className="toolbar">
        <input
          className="text-input"
          placeholder="Search by item or UOM code"
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
      {loading ? <p className="feedback">Loading item UOM conversions...</p> : null}

      {!loading ? (
        <div className="data-table">
          <div className="data-row data-row--conversions data-row--head">
            <span>Item</span>
            <span>Pair</span>
            <span>Factor</span>
            <span>Rounding</span>
            <span>Status</span>
            <span>Actions</span>
          </div>
          {rows.map((row) => (
            <div key={row.id} className="data-row data-row--conversions">
              <span>
                <strong>{row.itemName}</strong>
                <small>{row.itemCode}</small>
              </span>
              <span>
                <strong>{row.fromUomCode} → {row.toUomCode}</strong>
                <small>Min fraction: {row.minFraction}</small>
              </span>
              <span>{row.factor}</span>
              <span>{row.roundingMode}</span>
              <span className={row.isActive ? "status-pill active" : "status-pill inactive"}>
                {row.isActive ? "Active" : "Inactive"}
              </span>
              <span className="row-actions">
                <Link className="text-link-dark" to={`/item-uom-conversions/${row.id}/edit`}>
                  Edit
                </Link>
                {row.isActive ? (
                  <button className="secondary-button" onClick={() => void handleDeactivate(row.id)} type="button">
                    Deactivate
                  </button>
                ) : null}
              </span>
            </div>
          ))}
          {!rows.length ? <p className="feedback">No conversions found for the current filter.</p> : null}
        </div>
      ) : null}
    </section>
  );
}

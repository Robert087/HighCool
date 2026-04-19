import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { deactivateUom, listUoms, type Uom } from "../services/masterDataApi";

export function UomsPage() {
  const [uoms, setUoms] = useState<Uom[]>([]);
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
        const result = await listUoms(search, status);

        if (active) {
          setUoms(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load UOMs.");
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
      await deactivateUom(id);
      setUoms((current) =>
        current.map((uom) => (uom.id === id ? { ...uom, isActive: false } : uom)),
      );
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate UOM.");
    }
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>Units of Measure</h2>
          <p className="page-copy">Manage the controlled UOM catalogue used by future item and conversion rules.</p>
        </div>
        <Link className="primary-button" to="/uoms/new">
          New UOM
        </Link>
      </div>

      <div className="toolbar">
        <input
          className="text-input"
          placeholder="Search by code or name"
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
      {loading ? <p className="feedback">Loading UOMs...</p> : null}

      {!loading ? (
        <div className="data-table">
          <div className="data-row data-row--head">
            <span>UOM</span>
            <span>Precision</span>
            <span>Fractions</span>
            <span>Status</span>
            <span>Actions</span>
          </div>
          {uoms.map((uom) => (
            <div key={uom.id} className="data-row">
              <span>
                <strong>{uom.name}</strong>
                <small>{uom.code}</small>
              </span>
              <span>{uom.precision}</span>
              <span>{uom.allowsFraction ? "Allowed" : "Whole numbers only"}</span>
              <span className={uom.isActive ? "status-pill active" : "status-pill inactive"}>
                {uom.isActive ? "Active" : "Inactive"}
              </span>
              <span className="row-actions">
                <Link className="text-link-dark" to={`/uoms/${uom.id}/edit`}>
                  Edit
                </Link>
                {uom.isActive ? (
                  <button className="secondary-button" onClick={() => void handleDeactivate(uom.id)} type="button">
                    Deactivate
                  </button>
                ) : null}
              </span>
            </div>
          ))}
          {!uoms.length ? <p className="feedback">No UOMs found for the current filter.</p> : null}
        </div>
      ) : null}
    </section>
  );
}

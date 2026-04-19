import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { deleteItemComponent, listItemComponents, type ItemComponent } from "../services/masterDataApi";

export function ItemComponentsPage() {
  const [rows, setRows] = useState<ItemComponent[]>([]);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listItemComponents(search);
        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load item components.");
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
  }, [search]);

  async function handleDelete(id: string) {
    try {
      await deleteItemComponent(id);
      setRows((current) => current.filter((row) => row.id !== id));
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to delete item component.");
    }
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>Item Components</h2>
          <p className="page-copy">Define parent-item to component-item relationships and required quantities.</p>
        </div>
        <Link className="primary-button" to="/item-components/new">
          New component row
        </Link>
      </div>

      <div className="toolbar">
        <input
          className="text-input"
          placeholder="Search by parent or component item"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
      </div>

      {error ? <p className="feedback error">{error}</p> : null}
      {loading ? <p className="feedback">Loading item components...</p> : null}

      {!loading ? (
        <div className="data-table">
          <div className="data-row data-row--components data-row--head">
            <span>Parent Item</span>
            <span>Component Item</span>
            <span>Quantity</span>
            <span>Actions</span>
          </div>
          {rows.map((row) => (
            <div key={row.id} className="data-row data-row--components">
              <span>
                <strong>{row.parentItemName}</strong>
                <small>{row.parentItemCode}</small>
              </span>
              <span>
                <strong>{row.componentItemName}</strong>
                <small>{row.componentItemCode}</small>
              </span>
              <span>{row.quantity}</span>
              <span className="row-actions">
                <Link className="text-link-dark" to={`/item-components/${row.id}/edit`}>
                  Edit
                </Link>
                <button className="secondary-button" onClick={() => void handleDelete(row.id)} type="button">
                  Delete
                </button>
              </span>
            </div>
          ))}
          {!rows.length ? <p className="feedback">No item components found for the current filter.</p> : null}
        </div>
      ) : null}
    </section>
  );
}

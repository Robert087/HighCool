import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { deactivateItem, listItems, type Item } from "../services/masterDataApi";

export function ItemsPage() {
  const [items, setItems] = useState<Item[]>([]);
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
        const result = await listItems(search, status);

        if (active) {
          setItems(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load items.");
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
      await deactivateItem(id);
      setItems((current) => current.map((item) => (item.id === id ? { ...item, isActive: false } : item)));
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate item.");
    }
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>Items</h2>
          <p className="page-copy">Maintain sellable items, component items, and items that can play both roles.</p>
        </div>
        <Link className="primary-button" to="/items/new">
          New item
        </Link>
      </div>

      <div className="toolbar">
        <input
          className="text-input"
          placeholder="Search by code, name, or base UOM"
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
      {loading ? <p className="feedback">Loading items...</p> : null}

      {!loading ? (
        <div className="data-table">
          <div className="data-row data-row--head data-row--items">
            <span>Item</span>
            <span>Base UOM</span>
            <span>Roles</span>
            <span>Status</span>
            <span>Actions</span>
          </div>
          {items.map((item) => (
            <div key={item.id} className="data-row data-row--items">
              <span>
                <strong>{item.name}</strong>
                <small>{item.code}</small>
              </span>
              <span>
                <strong>{item.baseUomCode}</strong>
                <small>{item.baseUomName}</small>
              </span>
              <span>
                <small>{item.isSellable ? "Sellable" : "Not sellable"}</small>
                <small>{item.isComponent ? "Component" : "Not component"}</small>
              </span>
              <span className={item.isActive ? "status-pill active" : "status-pill inactive"}>
                {item.isActive ? "Active" : "Inactive"}
              </span>
              <span className="row-actions">
                <Link className="text-link-dark" to={`/items/${item.id}/edit`}>
                  Edit
                </Link>
                {item.isActive ? (
                  <button className="secondary-button" onClick={() => void handleDeactivate(item.id)} type="button">
                    Deactivate
                  </button>
                ) : null}
              </span>
            </div>
          ))}
          {!items.length ? <p className="feedback">No items found for the current filter.</p> : null}
        </div>
      ) : null}
    </section>
  );
}

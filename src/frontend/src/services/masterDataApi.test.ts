import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { requestJson } from "./api";
import { getActiveItemsCached } from "./masterDataApi";

vi.mock("./api", () => ({
  requestJson: vi.fn(),
}));

const requestJsonMock = vi.mocked(requestJson);

describe("masterDataApi active option loading", () => {
  beforeEach(() => {
    requestJsonMock.mockReset();
    vi.stubGlobal("window", { location: { origin: "http://localhost" } });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("loads active item options across all paged API results", async () => {
    requestJsonMock
      .mockResolvedValueOnce({
        items: [{ id: "item-1", code: "I-001", name: "First" }],
        page: 1,
        pageSize: 100,
        totalCount: 2,
        totalPages: 2,
        appliedFilters: {},
        sort: { sortBy: "name", direction: "Asc" },
      })
      .mockResolvedValueOnce({
        items: [{ id: "item-2", code: "I-002", name: "Second" }],
        page: 2,
        pageSize: 100,
        totalCount: 2,
        totalPages: 2,
        appliedFilters: {},
        sort: { sortBy: "name", direction: "Asc" },
      });

    const rows = await getActiveItemsCached();

    expect(rows.map((row) => row.id)).toEqual(["item-1", "item-2"]);
    expect(requestJsonMock).toHaveBeenCalledTimes(2);
    expect(requestJsonMock).toHaveBeenNthCalledWith(1, "/api/items?isActive=true&page=1&pageSize=100&sortBy=name&sortDirection=Asc");
    expect(requestJsonMock).toHaveBeenNthCalledWith(2, "/api/items?isActive=true&page=2&pageSize=100&sortBy=name&sortDirection=Asc");
  });
});
